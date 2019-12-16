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

    using SecretSanta.Configs;
    using SecretSanta.Db;

    public class Program {
        private static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "secretsanta");

        private static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };

            JsonConvert.DefaultSettings = () => jsonSerializerSettings;

            var config = LoadConfig();
            var database = LoadDatabase();

            var selectedGroup = ShowGroupsMenu(config.Groups);

            Console.WriteLine($"Group: {selectedGroup.Name}\n");

            Console.WriteLine("People:\n");
            MoreEnumerable.ForEach(selectedGroup.People.OrderBy(o => o.Name), f => Console.WriteLine($"{f.Name} <{f.Email}>"));

            var option = ShowMainMenu();

            var lotteries = database.Lotteries.Where(w => w.GroupId == selectedGroup.Id).ToList();

            switch (option) {
                case 1:
                    GenerateBranch(selectedGroup, lotteries, database, config.Email, config.User, config.Password);
                    break;

                case 2:
                    ReSendEmailsBranch(selectedGroup, lotteries, config.Email, config.User, config.Password);
                    break;

                case 3:
                    TestBranch(selectedGroup.People, lotteries);
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

        private static Group ShowGroupsMenu(IList<Group> groups) {
            while (true) {
                var option = GroupsMenu(groups);

                if (option != null) {
                    return option;
                }
            }
        }

        private static Group GroupsMenu(IList<Group> groups) {
            Console.WriteLine("\nGroups:");

            for (int i = 0; i < groups.Count; i++) {
                Console.WriteLine($"{i + 1}) {groups[i].Name}");
            }

            Console.Write("\nSelect an option and press 'Enter': ");

            var input = Console.ReadLine();

            Console.WriteLine("\n");

            try {
                var index = int.Parse(input);

                return groups[index - 1];
            }
            catch {
                return null;
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

            var input = Console.ReadLine();

            Console.WriteLine("\n");

            switch (input) {
                case "1":
                    return 1;
                case "2":
                    return 2;
                case "3":
                    return 3;
                default:
                    return null;
            }
        }

        private static void GenerateBranch(Group group, IList<LotteryDb> lotteries, Database database, string email, string user, string password) {
            Console.WriteLine("Generating secret santa...\n");

            var matches = DoLotterySafe(group.People, lotteries);

            var emailCode = DateTime.Now.ToShortTimeString();

            SendEmails(email, user, password, matches, emailCode, group.Name);

            SaveMatches(database, group.Id, matches, emailCode);
        }

        private static void ReSendEmailsBranch(Group group, IList<LotteryDb> lotteries, string email, string user, string password) {
            var lastLottery = lotteries.OrderBy(o => o.DateTime).LastOrDefault();

            if (lastLottery != null) {
                var matches = lastLottery.Matches.Select(
                        s => new Match() { Person = group.People.FirstOrDefault(f => f.Name == s.Source), Destination = s.Destination })
                    .ToList();

                SendEmails(email, user, password, matches, lastLottery.EmailCode, group.Name);
            } else {
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

        private static void SendEmails(string email, string user, string password, IList<Match> matches, string emailCode, string groupName) {
            MoreEnumerable.ForEach(
                matches,
                f => SendEmail(email, user, password, f.Person, f.Destination, emailCode, groupName));
        }

        public static void SendEmail(string email, string user, string password, Person person, string secretFriend, string code, string groupName) {
            Console.Write("Sending email to " + person.Email + " ... ");

            var mailMessage = new MailMessage { From = new MailAddress(email, "Secret Santa") };

            var emails = person.Email.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

            ////mailMessage.To.Add(new MailAddress(email));

            foreach (var personEmail in emails) {
                mailMessage.To.Add(new MailAddress(personEmail));
            }

            mailMessage.Subject = $"Amigo Invisible {groupName} (#{code})";
            mailMessage.IsBodyHtml = true;

            mailMessage.Body = person.Name + ":\n\nTu amigo invisible es: " + secretFriend.ToUpper();

            var client = new SmtpClient("smtp.sendgrid.net", 587) {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, password),
            };

            client.Send(mailMessage);

            Console.WriteLine("✔");
        }

        private static void SaveMatches(Database database, Guid groupId, IEnumerable<Match> matches, string emailCode) {
            Console.Write("\nSaving matches... ");

            var file = new FileInfo(Path.Combine(DataDirectory, "data.json"));

            database.Lotteries.Add(
                new LotteryDb(groupId, DateTime.Now, matches.Select(s => new MatchDb(s.Person.Name, s.Destination)).ToList(), emailCode));

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
                } else if (info.Key == ConsoleKey.Backspace) {
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
