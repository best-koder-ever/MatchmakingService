using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class GenderFilterTests : FilterTestBase
{
    private readonly GenderFilter _filter = new();

    [Fact]
    public void MaleSeekingFemale_OnlyShowsFemalesSeekingMale()
    {
        var user = CreateUser(userId: 1, gender: "Male", preferredGender: "Female");
        var candidates = CreateCandidates(
            CreateUser(userId: 2, gender: "Female", preferredGender: "Male"),
            CreateUser(userId: 3, gender: "Female", preferredGender: "Female"),  // doesn't want males
            CreateUser(userId: 4, gender: "Male", preferredGender: "Female"));   // wrong gender
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
        Assert.Equal(2, result[0].UserId);
    }

    [Fact]
    public void UserPreferenceEveryone_ShowsAllGenders()
    {
        var user = CreateUser(userId: 1, gender: "Male", preferredGender: "Everyone");
        var candidates = CreateCandidates(
            CreateUser(userId: 2, gender: "Female", preferredGender: "Male"),
            CreateUser(userId: 3, gender: "Male", preferredGender: "Everyone"),
            CreateUser(userId: 4, gender: "NonBinary", preferredGender: "Everyone"));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void CandidatePreferenceEveryone_AcceptsAllUserGenders()
    {
        var user = CreateUser(userId: 1, gender: "NonBinary", preferredGender: "Everyone");
        var candidates = CreateCandidates(
            CreateUser(userId: 2, gender: "Male", preferredGender: "Everyone"));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void SameSexPreference_Works()
    {
        var user = CreateUser(userId: 1, gender: "Male", preferredGender: "Male");
        var candidates = CreateCandidates(
            CreateUser(userId: 2, gender: "Male", preferredGender: "Male"),
            CreateUser(userId: 3, gender: "Female", preferredGender: "Male"));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
        Assert.Equal(2, result[0].UserId);
    }

    [Fact]
    public void EmptyPreference_TreatedAsEveryone()
    {
        var user = CreateUser(userId: 1, gender: "Male", preferredGender: "");
        var candidates = CreateCandidates(
            CreateUser(userId: 2, gender: "Female", preferredGender: ""));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
    }
}
