#!/usr/bin/env python3
"""FatSecret spike: verify the OAuth 1.0 three-legged flow and the monthly
food-entries / weights endpoints with YOUR developer key.

Usage:
    export FATSECRET_CONSUMER_KEY=...
    export FATSECRET_CONSUMER_SECRET=...
    python3 scripts/fatsecret_spike.py            # auth (first run) then fetch last 3 months
    python3 scripts/fatsecret_spike.py --reauth   # ignore the saved token and log in again
    python3 scripts/fatsecret_spike.py --reauth --callback   # test the redirect flow (no PIN)
    python3 scripts/fatsecret_spike.py --months 6 # fetch more months
    python3 scripts/fatsecret_spike.py --raw      # also print raw JSON responses

Every day of a period, printed and saved as CSV (git-ignored data/ folder by default):
    python3 scripts/fatsecret_spike.py --year                             # the last 12 months, today included
    python3 scripts/fatsecret_spike.py --from 2025-01-01 --to 2025-12-31  # any period
    python3 scripts/fatsecret_spike.py --year --csv ~/fatsecret.csv       # choose the file

Only the Python standard library is used. The access token/secret are saved to
scripts/.fatsecret-token.json (git-ignored, chmod 600). The consumer key/secret
are only read from the environment and never written anywhere.
"""
import argparse
import base64
import csv
import datetime as dt
import hashlib
import hmac
import json
import os
import secrets
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

AUTH_BASE = "https://authentication.fatsecret.com/oauth"
API_BASE = "https://platform.fatsecret.com/rest"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TOKEN_FILE = os.path.join(SCRIPT_DIR, ".fatsecret-token.json")
DATA_DIR = os.path.join(os.path.dirname(SCRIPT_DIR), "data")
EPOCH = dt.date(1970, 1, 1)


def enc(s):
    return urllib.parse.quote(str(s), safe="~")


def sign(method, url, params, consumer_secret, token_secret=""):
    norm = "&".join(f"{enc(k)}={enc(v)}" for k, v in sorted(params.items()))
    base = "&".join([method.upper(), enc(url), enc(norm)])
    key = f"{enc(consumer_secret)}&{enc(token_secret)}"
    digest = hmac.new(key.encode(), base.encode(), hashlib.sha1).digest()
    return base64.b64encode(digest).decode()


def oauth_call(method, url, extra, consumer_key, consumer_secret, token=None, token_secret=""):
    params = {
        "oauth_consumer_key": consumer_key,
        "oauth_signature_method": "HMAC-SHA1",
        "oauth_timestamp": str(int(time.time())),
        "oauth_nonce": secrets.token_hex(8),
        "oauth_version": "1.0",
        **extra,
    }
    if token:
        params["oauth_token"] = token
    params["oauth_signature"] = sign(method, url, params, consumer_secret, token_secret)
    query = "&".join(f"{enc(k)}={enc(v)}" for k, v in params.items())
    if method.upper() == "GET":
        req = urllib.request.Request(f"{url}?{query}", method="GET")
    else:
        req = urllib.request.Request(url, data=query.encode(), method="POST",
                                     headers={"Content-Type": "application/x-www-form-urlencoded"})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def wait_for_callback(port):
    """Start a one-shot local server and return the query params of the first request."""
    import http.server
    got = {}

    class H(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            got.update(dict(urllib.parse.parse_qsl(urllib.parse.urlparse(self.path).query)))
            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.end_headers()
            self.wfile.write(b"FatSecret callback received. You can close this tab.")

        def log_message(self, *a):
            pass

    srv = http.server.HTTPServer(("127.0.0.1", port), H)
    while "oauth_verifier" not in got:
        srv.handle_request()
    srv.server_close()
    return got


def authenticate(key, secret, callback_port=None):
    callback = f"http://localhost:{callback_port}/callback" if callback_port else "oob"
    print(f"Step 1/3: requesting a request token (callback = {callback})...")
    status, body = 0, ""
    for method in ("POST", "GET"):
        status, body = oauth_call(method, f"{AUTH_BASE}/request_token", {"oauth_callback": callback}, key, secret)
        if status == 200:
            break
        print(f"  request_token via {method} failed ({status}): {body}")
    if status != 200:
        print(f"  (consumer key length={len(key)}, secret length={len(secret)}; both POST and GET failed)")
        sys.exit("Check you are using the OAuth 1.0 Consumer Key/Secret (not the OAuth 2.0 Client ID/Secret) "
                 "and that there is no stray whitespace/quote in the env vars.")
    rt = dict(urllib.parse.parse_qsl(body))
    print("Step 2/3: open this URL in a browser, log in to FatSecret and approve:\n")
    print(f"  {AUTH_BASE}/authorize?oauth_token={rt['oauth_token']}\n")
    if callback_port:
        print(f"Waiting for FatSecret to redirect your browser to {callback} ...")
        got = wait_for_callback(callback_port)
        print(f"  callback received; params: {sorted(got)}")
        if got.get("oauth_token") != rt["oauth_token"]:
            sys.exit("Callback oauth_token does not match the request token.")
        verifier = got["oauth_verifier"]
    else:
        verifier = input("Enter the verification code (PIN) shown by FatSecret: ").strip()
    print("Step 3/3: exchanging for an access token...")
    status, body = oauth_call("GET", f"{AUTH_BASE}/access_token", {"oauth_verifier": verifier},
                              key, secret, rt["oauth_token"], rt["oauth_token_secret"])
    if status != 200:
        sys.exit(f"access_token failed ({status}): {body}")
    at = dict(urllib.parse.parse_qsl(body))
    data = {"token": at["oauth_token"], "secret": at["oauth_token_secret"]}
    fd = os.open(TOKEN_FILE, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w") as f:
        json.dump(data, f)
    print(f"Saved access token to {TOKEN_FILE}\n")
    return data


def load_token():
    try:
        with open(TOKEN_FILE) as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def month_starts(n):
    d = dt.date.today().replace(day=1)
    out = []
    for _ in range(n):
        out.append(d)
        d = (d - dt.timedelta(days=1)).replace(day=1)
    return out


def as_list(x):
    return x if isinstance(x, list) else ([x] if x else [])


def fetch_month(path, month_start, key, secret, tok, raw):
    date_int = (month_start - EPOCH).days
    status, body = oauth_call("GET", f"{API_BASE}/{path}", {"date": str(date_int), "format": "json"},
                              key, secret, tok["token"], tok["secret"])
    if raw:
        print(f"[raw {path} {month_start:%Y-%m}] HTTP {status}: {body}")
    try:
        data = json.loads(body)
    except ValueError:
        return status, None, body
    if "error" in data:
        return status, None, data["error"]
    return status, as_list(data.get("month", {}).get("day")), None


def explain_error(err):
    code = str(err.get("code")) if isinstance(err, dict) else ""
    hints = {
        "21": "IP not whitelisted: add your PUBLIC IP under 'Manage API Keys' on platform.fatsecret.com (localhost/LAN IPs do not work).",
        "9": "Invalid access token: rerun with --reauth.",
        "6": "Invalid/expired timestamp: check this machine's clock.",
        "8": "Invalid signature: check consumer key/secret.",
    }
    return hints.get(code, "")


def one_year_before(d):
    try:
        return d.replace(year=d.year - 1)
    except ValueError:  # 29 February
        return d.replace(year=d.year - 1, day=28)


def months_between(start, end):
    """First day of every month the period touches, oldest first."""
    d = start.replace(day=1)
    out = []
    while d <= end:
        out.append(d)
        d = (d + dt.timedelta(days=31)).replace(day=1)
    return out


def number(value, decimals):
    """FatSecret sends numbers as strings. Whole kcal, kg with up to 2 decimals."""
    n = round(float(value), decimals)
    return int(n) if n == int(n) and decimals == 0 else n


def fetch_period(start, end, key, secret, tok, raw):
    """{date: {"calories": kcal or None, "weight_kg": kg or None}} for every day of the period, and the call count."""
    days = {start + dt.timedelta(days=i): {"calories": None, "weight_kg": None} for i in range((end - start).days + 1)}
    calls = 0
    for ms in months_between(start, end):
        print(f"  fetching {ms:%Y-%m}...", file=sys.stderr)
        for path, field, decimals in (("food-entries/month/v2", "calories", 0), ("weight/month/v2", "weight_kg", 2)):
            status, entries, err = fetch_month(path, ms, key, secret, tok, raw)
            calls += 1
            if entries is None:
                # Never save a period with holes in it: a missing month would look like days without a log.
                hint = explain_error(err)
                sys.exit(f"{path} for {ms:%Y-%m} failed, nothing was saved: HTTP {status}: {err}" + (f"\n  -> {hint}" if hint else ""))
            for e in entries:
                date = EPOCH + dt.timedelta(days=int(e["date_int"]))
                if date in days and e.get(field) not in (None, ""):
                    days[date][field] = number(e[field], decimals)
    return days, calls


def export_period(start, end, csv_path, key, secret, tok, raw):
    days, calls = fetch_period(start, end, key, secret, tok, raw)

    print("date        calories  weight_kg")
    for date, d in days.items():
        kcal = "" if d["calories"] is None else d["calories"]
        kg = "" if d["weight_kg"] is None else f"{d['weight_kg']:.2f}"
        print(f"{date}  {kcal:>8}  {kg:>9}")

    os.makedirs(os.path.dirname(os.path.abspath(csv_path)), exist_ok=True)
    with open(csv_path, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["date", "calories_kcal", "weight_kg"])
        for date, d in days.items():
            w.writerow([date.isoformat(), "" if d["calories"] is None else d["calories"],
                        "" if d["weight_kg"] is None else d["weight_kg"]])

    kcals = [d["calories"] for d in days.values() if d["calories"] is not None]
    kgs = [d["weight_kg"] for d in days.values() if d["weight_kg"] is not None]
    print(f"\n{start} to {end}: {len(days)} days")
    print(f"  calories: {len(kcals)} day(s)" + (f", average {sum(kcals) / len(kcals):.0f} kcal" if kcals else ""))
    print(f"  weight:   {len(kgs)} day(s)" + (f", {kgs[0]:.2f} -> {kgs[-1]:.2f} kg" if kgs else ""))
    print(f"Saved to {os.path.abspath(csv_path)} ({calls} API calls)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--reauth", action="store_true")
    ap.add_argument("--months", type=int, default=3)
    ap.add_argument("--raw", action="store_true")
    ap.add_argument("--callback", nargs="?", const=8765, type=int, metavar="PORT",
                    help="use a local http://localhost:PORT/callback redirect instead of the PIN (default port 8765)")
    ap.add_argument("--year", action="store_true", help="every day of the last 12 months, printed and saved as CSV")
    ap.add_argument("--from", dest="start", type=dt.date.fromisoformat, metavar="YYYY-MM-DD", help="start of a period")
    ap.add_argument("--to", dest="end", type=dt.date.fromisoformat, metavar="YYYY-MM-DD", help="end of a period (default today)")
    ap.add_argument("--csv", metavar="FILE", help="where to save the period (default data/fatsecret-FROM_TO.csv)")
    args = ap.parse_args()

    today = dt.date.today()
    if args.year and (args.start or args.end):
        ap.error("--year already sets the period; use --from/--to alone for another one")
    if args.end and not args.start:
        ap.error("--to needs --from")
    start = one_year_before(today) if args.year else args.start
    end = min(args.end or today, today)
    if args.csv and not start:
        ap.error("--csv needs --year or --from")
    if start and start > end:
        ap.error("--from is after --to (or in the future)")

    key = os.environ.get("FATSECRET_CONSUMER_KEY", "").strip().strip("'\"")
    secret = os.environ.get("FATSECRET_CONSUMER_SECRET", "").strip().strip("'\"")
    if not key or not secret:
        sys.exit("Set FATSECRET_CONSUMER_KEY and FATSECRET_CONSUMER_SECRET in the environment.")

    tok = None if args.reauth else load_token()
    if tok:
        print(f"Using saved access token ({TOKEN_FILE}); use --reauth to log in again.\n")
    else:
        tok = authenticate(key, secret, args.callback)

    if start:
        csv_path = os.path.expanduser(args.csv) if args.csv else os.path.join(DATA_DIR, f"fatsecret-{start}_{end}.csv")
        export_period(start, end, csv_path, key, secret, tok, args.raw)
        return

    calls = 0
    for ms in month_starts(args.months):
        print(f"=== {ms:%Y-%m} ===")
        for label, path, field, unit in (
            ("calories", "food-entries/month/v2", "calories", "kcal"),
            ("weight", "weight/month/v2", "weight_kg", "kg"),
        ):
            status, days, err = fetch_month(path, ms, key, secret, tok, args.raw)
            calls += 1
            if days is None:
                print(f"  {label}: ERROR HTTP {status}: {err}")
                hint = explain_error(err)
                if hint:
                    print(f"    -> {hint}")
                continue
            print(f"  {label}: {len(days)} day(s) with data")
            for d in days[:3] + (["..."] if len(days) > 3 else []):
                if d == "...":
                    print("    ...")
                    continue
                date = EPOCH + dt.timedelta(days=int(d["date_int"]))
                print(f"    {date}  {d.get(field)} {unit}")
    print(f"\nDone. {calls} API calls made "
          f"(12h sync of {args.months} months = {args.months * 2 * 2}/day, limit is 5000/day).")


if __name__ == "__main__":
    main()
