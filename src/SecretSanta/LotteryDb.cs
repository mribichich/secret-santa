namespace SecretSanta {
    using System;
    using System.Collections.Generic;

    public class LotteryDb {
        public LotteryDb(DateTime dateTime, List<MatchDb> matches, string emailCode) {
            this.DateTime = dateTime;
            this.Matches = matches;
            this.EmailCode = emailCode;
        }

        public DateTime DateTime { get; set; }

        public List<MatchDb> Matches { get; set; }

        public string EmailCode { get; set; }
    }
}