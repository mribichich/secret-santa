namespace SecretSanta.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Xunit;

    public class CreateBolillasForPersonTests {
        [Fact]
        public void TestMethod1() {
            var lotteries = new List<LotteryDb>();

            var result = Program.CreateBolillasForPerson(new List<string>() { "neitana", "marce", "juli" }, lotteries, "juli");

            Assert.Equal(result.OrderBy(o => o), new List<string> { "marce", "neitana", });
        }

        [Fact]
        public void TestMethod2() {
            var lotteries = new List<LotteryDb>()
                {
                    new LotteryDb(new DateTime(2016, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitana"), }, "1"),
                    new LotteryDb(new DateTime(2017, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitana"), }, "1"),
                    new LotteryDb(new DateTime(2018, 12, 01), new List<MatchDb>() { new MatchDb("juli", "guli"), }, "1"),
                };

            var result = Program.CreateBolillasForPerson(new List<string>() { "neitana", "marce", "juli" }, lotteries, "juli");

            Assert.Equal(result.OrderBy(o => o), new[] { "marce", "marce", "marce", "neitana", });
        }

        [Fact]
        public void TestMethod3() {
            var lotteries = new List<LotteryDb>()
                {
                    new LotteryDb(new DateTime(2016, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitana"), }, "1"),
                };

            var result = Program.CreateBolillasForPerson(new List<string>() { "neitana", "marce", "juli" }, lotteries, "juli");

            Assert.Equal(result.OrderBy(o => o), new List<string> { "marce", });
        }

        [Fact]
        public void TestMethod4() {
            var lotteries = new List<LotteryDb>()
                {
                    new LotteryDb(new DateTime(2016, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitan"), }, "1"),
                    new LotteryDb(new DateTime(2017, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitana"), }, "1"),
                    new LotteryDb(new DateTime(2018, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitan"), }, "1"),
                    new LotteryDb(new DateTime(2019, 12, 01), new List<MatchDb>() { new MatchDb("juli", "guli"), }, "1"),
                };

            var result = Program.CreateBolillasForPerson(new List<string>() { "neitana", "marce", "juli" }, lotteries, "juli");

            Assert.Equal(result.OrderBy(o => o), new List<string> { "marce", "marce", "neitana", });
        }

        [Fact]
        public void TestMethod5() {
            var lotteries = new List<LotteryDb>()
                {
                    new LotteryDb(new DateTime(2016, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitan"), }, "1"),
                    new LotteryDb(new DateTime(2017, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitana"), }, "1"),
                    new LotteryDb(new DateTime(2018, 12, 01), new List<MatchDb>() { new MatchDb("juli", "neitan"), }, "1"),
                    new LotteryDb(new DateTime(2019, 12, 01), new List<MatchDb>() { new MatchDb("juli", "guli"), }, "1"),
                };

            var result = Program.CreateBolillasForPerson(new List<string>() { "neitana", "marce", "juli", "neitan" }, lotteries, "juli");

            Assert.Equal(
                result.OrderBy(o => o),
                new List<string>
                    {
                        "marce",
                        "marce",
                        "marce",
                        "neitan",
                        "neitana",
                        "neitana",
                    });
        }
    }
}