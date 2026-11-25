#!/usr/bin/env python3
"""Merge an app export (JSON) with a FatSecret CSV into a new, importable export.

Rules, per day and per field (calories, weight):
  - export empty                      -> FatSecret value (if any)
  - export calories == 1800 (default) -> FatSecret value (if any); 1800 is the planned value
  - both set and different            -> conflict, listed in conflicts.csv
  - otherwise                         -> export value
Days that exist only in the CSV are added. Everything else in the export
(settings, goals, notes, cheat days...) is kept untouched.

Step 1: run it. It writes merged.json (conflicts resolved with the `choice`
        column default, "fatsecret") and conflicts.csv.
Step 2: edit the `choice` column of conflicts.csv ("export", "fatsecret", or any
        number of your own), then run the script again: it re-reads conflicts.csv.
Step 3: import merged.json in the app.

A calories value of 0 is a real value; only an empty cell means "not logged".

    python3 merge_fatsecret_export.py EXPORT.json FATSECRET.csv [--out DIR]
"""
import argparse
import csv
import json
import os
import sys
from datetime import datetime, timezone

PLANNED = 1800


def num(s):
    s = (s or "").strip().replace(",", ".")
    return float(s) if s else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("export")
    ap.add_argument("csv")
    ap.add_argument("--out", default=r"./worktree" if os.name == "nt"
                    else "./worktree")
    ap.add_argument("--planned", type=int, default=PLANNED, help="calories value treated as 'prefer FatSecret'")
    a = ap.parse_args()

    with open(a.export, encoding="utf-8") as f:
        data = json.load(f)
    logs = {l["date"]: l for l in data["dailyLogs"]}

    fs = {}
    with open(a.csv, encoding="utf-8", newline="") as f:
        for r in csv.DictReader(f):
            kcal = num(r["calories_kcal"])
            w = num(r["weight_kg"])
            if kcal is not None or w is not None:
                fs[r["date"]] = (None if kcal is None else int(round(kcal)), w)

    os.makedirs(a.out, exist_ok=True)
    cpath = os.path.join(a.out, "conflicts.csv")
    choices = {}
    if os.path.exists(cpath):
        with open(cpath, encoding="utf-8", newline="") as f:
            for r in csv.DictReader(f):
                choices[(r["date"], r["field"])] = r["choice"].strip().lower()

    now = datetime.now(timezone.utc).isoformat()
    next_id = max([l.get("id", 0) for l in logs.values()] + [0]) + 1
    conflicts, stats = [], dict(added=0, filled=0, replaced_planned=0, conflicts=0)

    for date, (fk, fw) in sorted(fs.items()):
        log = logs.get(date)
        if log is None:
            log = dict(id=next_id, date=date, weightKg=None, caloriesKcal=None, notes=None,
                       calorieTarget=None, isCheatDay=False, createdAt=now, updatedAt=now)
            next_id += 1
            logs[date] = log
            stats["added"] += 1
        for field, fv in (("caloriesKcal", fk), ("weightKg", fw)):
            if fv is None:
                continue
            ev = log[field]
            if ev is None:
                log[field] = fv
                stats["filled"] += 1
            elif field == "caloriesKcal" and ev == a.planned and fv != ev:
                log[field] = fv
                stats["replaced_planned"] += 1
            elif abs(ev - fv) > 1e-9:
                key = (date, field)
                stats["conflicts"] += 1
                choice = choices.get(key, "fatsecret")
                if choice == "fatsecret":
                    log[field] = fv
                elif choice != "export":
                    log[field] = int(float(choice)) if field == "caloriesKcal" else float(choice)
                conflicts.append((date, field, ev, fv, choice))

    data["dailyLogs"] = sorted(logs.values(), key=lambda l: l["date"])
    mpath = os.path.join(a.out, "merged.json")
    with open(mpath, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    with open(cpath, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(["date", "field", "export_value", "fatsecret_value", "choice"])
        w.writerows(conflicts)

    print(f"added days: {stats['added']}, filled empty: {stats['filled']}, "
          f"{a.planned} replaced: {stats['replaced_planned']}, conflicts: {stats['conflicts']}")
    print(f"wrote {mpath}\nwrote {cpath} (edit the 'choice' column, then rerun)")


if __name__ == "__main__":
    sys.exit(main())
