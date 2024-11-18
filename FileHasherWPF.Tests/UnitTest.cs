using Xunit;
using FileHasherWPF.Model;

namespace FileHasherWPF.Tests
{
    public class UnitTest
    {
        [Fact]
        public void TestHashingString()
        {
            var sh = new StringHasher(Hasher.HashAlgos.MD5, "123456");
            Assert.Equal("E10ADC3949BA59ABBE56E057F20F883E", sh.HashResult);
            sh = new StringHasher(Hasher.HashAlgos.SHA1, "123456");
            Assert.Equal("7C4A8D09CA3762AF61E59520943DC26494F8941B", sh.HashResult);
            sh = new StringHasher(Hasher.HashAlgos.SHA256, "123456");
            Assert.Equal("8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92", sh.HashResult);
            sh = new StringHasher(Hasher.HashAlgos.SHA512, "123456");
            Assert.Equal("BA3253876AED6BC22D4A6FF53D8406C6AD864195ED144AB5C87621B6C233B548BAEAE6956DF346EC8C17F5EA10F35EE3CBC514797ED7DDD3145464E2A0BAB413", sh.HashResult);
        }
    }
}
