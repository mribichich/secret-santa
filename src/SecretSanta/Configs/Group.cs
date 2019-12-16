namespace SecretSanta.Configs {
    using System;
    using System.Collections.Generic;

    public class Group {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public List<Person> People { get; set; }
    }
}
