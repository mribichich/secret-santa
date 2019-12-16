namespace SecretSanta.Configs {
    using System.Collections.Generic;

    public class Config {
        public string Email { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        public List<Group> Groups { get; set; }
    }
}
