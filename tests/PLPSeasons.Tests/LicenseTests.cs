using PLPSeasons.Core;
using Xunit;

public class LicenseTests
{
    const string Pub = "7Z4T2Okklarz6mPdKgng/qEynTJfdbvs+9+9mLoCrR0=";
    static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1794993870);
    [Fact] public void LifetimeKeyIsValid() => Assert.True(License.Valid("PLP-eyJwcm9kdWN0IjoicGxwLXNub3ciLCJ2ZXJzaW9uIjoxLCJpZCI6InR4bl8wMWFiYyIsImlzc3VlZCI6MTc5NDk5Mzc3MH0.YMAx-fg4mfTEe-fNzQu_TzGgyiovFrOpK_aKdayGsoe2G_zCfQdg4-e3S_5lk3mwsQ5krGqBE-4d43hyMdIACw", Pub, Now));
    [Fact] public void SubscriptionKeyIsValidUntilExp() => Assert.True(License.Valid("PLP-eyJwcm9kdWN0IjoicGxwLXNlYXNvbnMiLCJ2ZXJzaW9uIjoyLCJpZCI6InN1Yl8xIiwiaXNzdWVkIjoxNzk0OTkzNzcwLCJleHAiOjE3OTUwODAyNzB9.JbrogFb1EP6bg45SlHrDGr37yLSjRhkj1sRUVfvoNYIOU1Z4sd_avArWnBj7U4MZN6YH4eM8ogMC8R-DQiQ9Cg", Pub, Now));
    [Fact] public void ExpiredKeyIsRejected() => Assert.False(License.Valid("PLP-eyJwcm9kdWN0IjoicGxwLXNlYXNvbnMiLCJ2ZXJzaW9uIjoyLCJpZCI6InN1Yl8xIiwiaXNzdWVkIjoxNzk0OTkyODcwLCJleHAiOjE3OTQ5OTM4NjB9.N48jQGyewZ_Zw_tQ0QYnyKSEfXmVegHCd-gE9bAXW-n-88TCBXPo58II6HCNB98DD4MOicWIaD2y_2hahFWPAw", Pub, Now));
    [Fact] public void TamperedKeyIsRejected() { var k = "PLP-eyJwcm9kdWN0IjoicGxwLXNub3ciLCJ2ZXJzaW9uIjoxLCJpZCI6InR4bl8wMWFiYyIsImlzc3VlZCI6MTc5NDk5Mzc3MH0.YMAx-fg4mfTEe-fNzQu_TzGgyiovFrOpK_aKdayGsoe2G_zCfQdg4-e3S_5lk3mwsQ5krGqBE-4d43hyMdIACw"; Assert.False(License.Valid(k.Substring(0, 20) + (k[20] == 'A' ? 'B' : 'A') + k.Substring(21), Pub, Now)); }
    [Fact] public void OtherPublicKeyRejects() => Assert.False(License.Valid("PLP-eyJwcm9kdWN0IjoicGxwLXNub3ciLCJ2ZXJzaW9uIjoxLCJpZCI6InR4bl8wMWFiYyIsImlzc3VlZCI6MTc5NDk5Mzc3MH0.YMAx-fg4mfTEe-fNzQu_TzGgyiovFrOpK_aKdayGsoe2G_zCfQdg4-e3S_5lk3mwsQ5krGqBE-4d43hyMdIACw", null, Now));
    [Fact] public void GarbageIsRejected() { Assert.False(License.Valid("", Pub)); Assert.False(License.Valid("PLP-abc.def", Pub)); }
}
