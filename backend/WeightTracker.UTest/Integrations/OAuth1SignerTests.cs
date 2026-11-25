using WeightTracker.Api.Integrations.FatSecret;

namespace WeightTracker.UTest.Integrations;

public class OAuth1SignerTests
{
    [Fact]
    public void Sign_MatchesTheExampleOfTheOAuthSpecification()
    {
        // OAuth Core 1.0, appendix A.5.
        var signature = OAuth1Signer.Sign("GET", "http://photos.example.net/photos", new Dictionary<string, string>
        {
            ["oauth_consumer_key"] = "dpf43f3p2l4k3l03",
            ["oauth_token"] = "nnch734d00sl2jdk",
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = "1191242096",
            ["oauth_nonce"] = "kllo9940pd9333jh",
            ["oauth_version"] = "1.0",
            ["file"] = "vacation.jpg",
            ["size"] = "original",
        }, "kd94hf93k423kf44", "pfkkdhi9sl3r4s00");

        Assert.Equal("tR3+Ty81lMeYAr/Fid0kMTYa/WM=", signature);
    }

    [Fact]
    public void Sign_EncodesLikeTheSpikeScript()
    {
        // Same input through scripts/fatsecret_spike.py, which is verified with a real key.
        var signature = OAuth1Signer.Sign("post", "https://api.example.com/a~b", new Dictionary<string, string>
        {
            ["oauth_callback"] = "http://localhost:8080/api/x?y=1&z=2",
            ["text"] = "Ladies + Gentlemen, (it's) 100%! * é ~",
        }, "c&s", "t s");

        Assert.Equal("iUyQ/lnSkIcHeUUneWYKMYu5w4s=", signature);
    }
}
