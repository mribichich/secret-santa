namespace SecretSanta {
    public class MatchDb
    {
        public MatchDb(string source, string destination) {
            this.Source = source;
            this.Destination = destination;
        }

        public string Source { get; set; }

        public string Destination { get; set; }
    }
}