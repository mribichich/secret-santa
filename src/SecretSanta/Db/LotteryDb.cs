namespace SecretSanta.Db {
    using System;
    using System.Collections.Generic;

    public class LotteryDb {
        public LotteryDb(Guid groupId, DateTime dateTime, List<MatchDb> matches, string emailCode) {
            this.GroupId = groupId;
            this.DateTime = dateTime;
            this.Matches = matches;
            this.EmailCode = emailCode;
        }

        public Guid GroupId { get; }

        public DateTime DateTime { get; set; }

        public List<MatchDb> Matches { get; set; }

        public string EmailCode { get; set; }
    }
}
