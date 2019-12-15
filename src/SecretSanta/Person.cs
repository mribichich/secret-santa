namespace SecretSanta {
    using System;

    public class Person {
        public string Name { get; set; }

        public string Email { get; set; }

        public string[] Exceptions { get; set; } = new string[0];
    }
}