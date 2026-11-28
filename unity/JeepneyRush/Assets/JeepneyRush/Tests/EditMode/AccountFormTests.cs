using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class AccountFormTests
    {
        [Test]
        public void CreateAccountForm()
        {
            Assert.IsNull(OnlineText.AccountProblem(" driver@gmail.com ", "secret1", "secret1"));
            StringAssert.Contains("email", OnlineText.AccountProblem("driver", "secret1", "secret1"));
            StringAssert.Contains("email", OnlineText.AccountProblem("driver@gmail", "secret1", "secret1"));
            StringAssert.Contains("6", OnlineText.AccountProblem("driver@gmail.com", "abc", "abc"));
            StringAssert.Contains("match", OnlineText.AccountProblem("driver@gmail.com", "secret1", "secret2"));
        }
    }
}
