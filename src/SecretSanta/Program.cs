namespace SecretSanta {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Mail;

    using MoreLinq;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class Program {
        private static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "secretsanta");

        private static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };

            JsonConvert.DefaultSettings = () => jsonSerializerSettings;

            var config = LoadConfig();
            var database = LoadDatabase();

            Console.WriteLine("\nPeople:\n");
            MoreEnumerable.ForEach(config.People.OrderBy(o => o.Name), f => Console.WriteLine($"{f.Name} <{f.Email}>"));

            var option = ShowMainMenu();

            switch (option) {
                case 1:
                    GenerateBranch(config.People, database, config.Email, config.User, config.Password);
                    break;

                case 2:
                    ReSendEmailsBranch(config.People, database.Lotteries, config.Email, config.User, config.Password);
                    break;

                case 3:
                    TestBranch(config.People, database.Lotteries);
                    break;
            }

            Console.WriteLine("\nDone!!!");

#if DEBUG
            Console.WriteLine("\nPress any key to exit...");
            Console.Read();
#endif
        }

        private static Config LoadConfig() {
            var file = new FileInfo(Path.Combine(DataDirectory, "config.json"));

            using (var stream = file.OpenText()) {
                var data = stream.ReadToEnd();
                return JsonConvert.DeserializeObject<Config>(data);
            }
        }

        private static Database LoadDatabase() {
            var file = new FileInfo(Path.Combine(DataDirectory, "data.json"));

            using (var stream = file.OpenText()) {
                var data = stream.ReadToEnd();
                return JsonConvert.DeserializeObject<Database>(data);
            }
        }

        private static int ShowMainMenu() {
            while (true) {
                var option = MainMenu();

                if (option.HasValue) {
                    return option.Value;
                }
            }
        }

        private static int? MainMenu() {
            Console.WriteLine("\nMenu:");
            Console.WriteLine("1) Generate");
            Console.WriteLine("2) Resend previous");
            Console.WriteLine("3) Test");
            Console.Write("\nSelect an option: ");

            var key = Console.ReadKey();

            Console.WriteLine("\n");

            switch (key.KeyChar) {
                case '1':
                    return 1;
                case '2':
                    return 2;
                case '3':
                    return 3;
                default:
                    return null;
            }
        }

        private static void GenerateBranch(IReadOnlyList<Person> people, Database database, string email, string user, string password) {
            Console.WriteLine("Generating secret santa...\n");

            var matches = DoLotterySafe(people, database.Lotteries);

            var emailCode = DateTime.Now.ToShortTimeString();

            SendEmails(email, user, password, matches, emailCode);

            SaveMatches(database, matches, emailCode);
        }

        private static void ReSendEmailsBranch(IReadOnlyList<Person> people, IList<LotteryDb> lotteries, string email, string user, string password) {
            var lastLottery = lotteries.OrderBy(o => o.DateTime).LastOrDefault();

            if (lastLottery != null) {
                var matches = lastLottery.Matches.Select(
                        s => new Match() { Person = people.FirstOrDefault(f => f.Name == s.Source), Destination = s.Destination })
                    .ToList();

                SendEmails(email, user, password, matches, lastLottery.EmailCode);
            }
            else {
                Console.WriteLine("No lotteries found\n");
            }
        }

        private static void TestBranch(IReadOnlyList<Person> people, IList<LotteryDb> lotteries) {
            Console.WriteLine("Testing secret santa...\n");

            var matches = DoLotterySafe(people, lotteries);

            Console.WriteLine("Matches:\n");

            matches.ForEach(m => Console.WriteLine($"{m.Person.Name}\t-> {m.Destination} " + "✔"));
        }

        public static List<string> CreateBolillasForPerson(IEnumerable<string> people, IList<LotteryDb> pastLotteries, string currentPerson) {
            var lastDestination = pastLotteries.OrderBy(o => o.DateTime)
                .LastOrDefault()
                ?.Matches.FirstOrDefault(f => f.Source == currentPerson)
                ?.Destination;
            var peopleToUse = people.Except(new[] { currentPerson, lastDestination });

            var pastOccurrences = pastLotteries.SelectMany(s => s.Matches.Where(s2 => s2.Source == currentPerson))
                .Where(w => !string.IsNullOrWhiteSpace(lastDestination) && w.Destination != lastDestination)
                .Where(w => peopleToUse.Contains(w.Destination))
                .GroupBy(g => g.Destination)
                .ToDictionary(d => d.Key, d => d.Count());

            var maxOccurrence = pastOccurrences.Any() ? pastOccurrences.Values.Max() : 0;

            return peopleToUse.SelectMany(s => Enumerable.Repeat(s, maxOccurrence + 1 - (pastOccurrences.ContainsKey(s) ? pastOccurrences[s] : 0)))
                .ToList();
        }

        private static List<Match> DoLotterySafe(IReadOnlyList<Person> people, IList<LotteryDb> lotteries) {
            while (true) {
                try {
                    var matches = DoLottery(people, lotteries);

                    CheckForConsistency(people, matches);

                    return matches;
                }
                catch {
                }
            }
        }

        private static List<Match> DoLottery(IReadOnlyList<Person> people, IList<LotteryDb> lotteries) {
            var peopleShuffled = ShuffleList.ShuffleTimes(2, people.ToList());

            var matches = new List<Match>();

            foreach (var person in peopleShuffled) {
                var peopleLeft = people.Select(s => s.Name).Except(person.Exceptions.Union(matches.Select(s => s.Destination)));
                var bolillas = CreateBolillasForPerson(peopleLeft, lotteries, person.Name);

                var bolillasShuffled = ShuffleList.ShuffleTimes(2, bolillas);

                var index = ShuffleList.Random(bolillasShuffled.Count);

                matches.Add(new Match() { Person = person, Destination = bolillasShuffled[index] });
            }

            return matches;
        }

        private static void CheckForConsistency(IReadOnlyList<Person> people, IList<Match> matches) {
            if (people.Count != matches.Count) {
                throw new Exception("Inconsistency check found: 'people.Count != matches.Count'");
            }

            if (!people.All(a => matches.Any(m => m.Person.Name == a.Name))) {
                throw new Exception("Inconsistency check found: 'all people not found in matches'");
            }

            if (!people.All(a => matches.Any(m => m.Destination == a.Name))) {
                throw new Exception("Inconsistency check found: 'all people not found in matches as bolillas'");
            }
        }

        private static void SendEmails(string email, string user, string password, IList<Match> matches, string emailCode) {
            MoreEnumerable.ForEach(
                matches,
                f =>
                    {
                        //Console.WriteLine($"{f.Person.Name}\t-> {f.Bolilla.Name}");

                        SendEmail(email, user, password, f.Person, f.Destination, emailCode);
                    });
        }

        public static void SendEmail(string email, string user, string password, Person person, string secretFriend, string code) {
            Console.Write("Sending email to " + person.Email + " ... ");

            var mailMessage = new MailMessage { From = new MailAddress(email, "Secret Santa SisBro") };

            var emails = person.Email.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

            ////mailMessage.To.Add(new MailAddress(email));

            foreach (var personEmail in emails) {
                mailMessage.To.Add(new MailAddress(personEmail));
            }

            mailMessage.Subject = $"Amigo Invisible Navidad SisBro (#{code})";
            mailMessage.IsBodyHtml = true;

            mailMessage.Body = person.Name + ":\n\nTu amigo invisible para navidad es: " + secretFriend.ToUpper();

            var client = new SmtpClient("smtp.sendgrid.net", 587)
                {
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(user, password),
                };

            client.Send(mailMessage);

            Console.WriteLine("✔");
        }

        private static void SaveMatches(Database database, List<Match> matches, string emailCode) {
            Console.Write("\nSaving matches... ");

            var file = new FileInfo(Path.Combine(DataDirectory, "data.json"));

            database.Lotteries.Add(new LotteryDb(DateTime.Now, matches.Select(s => new MatchDb(s.Person.Name, s.Destination)).ToList(), emailCode));

            using (var strem = file.CreateText()) {
                strem.Write(JsonConvert.SerializeObject(database));
            }

            Console.WriteLine("✔");
        }

        //public static void SortPeople(IReadOnlyList<Person> people) {
        //    for (var i = 0; i < 10; i++) {
        //        foreach (var person in people) {
        //            person.Order = Guid.NewGuid();
        //        }
        //    }
        //}

        //public static List<Bolilla> SortBolillas(List<Bolilla> bolillas) {
        //    for (var i = 0; i < 10; i++) {
        //        foreach (var bolilla in bolillas) {
        //            bolilla.Order = Guid.NewGuid();
        //        }
        //    }

        //    return bolillas.OrderBy(o => o.Order).ToList();
        //}

        //public static List<Bolilla> CreateBolillas(IReadOnlyList<Person> people) {
        //    return new List<Bolilla>(people.Select(s => new Bolilla() { Name = s.Name }));
        //}

        public static string Returnpassword() {
            var info = Console.ReadKey(true);

            var password = "";

            while (info.Key != ConsoleKey.Enter) {
                if (info.Key != ConsoleKey.Backspace) {
                    password += info.KeyChar;

                    Console.Write("*");

                    info = Console.ReadKey(true);
                }
                else if (info.Key == ConsoleKey.Backspace) {
                    if (!string.IsNullOrEmpty(password)) {
                        password = password.Substring(0, password.Length - 1);

                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.Write("\b");
                    }

                    info = Console.ReadKey(true);
                }
            }

            Console.WriteLine();

            return password;
        }
    }
}