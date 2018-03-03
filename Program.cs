using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp;
using AngleSharp.Parser.Html;

namespace FrequencyWords
{
    internal class Program
    {
        private static void Main()
        {
            var config = Configuration.Default.WithDefaultLoader();
            const string episodesListLink = "http://de.spongepedia.org/index.php/Episoden";
            var document = BrowsingContext.New(config).OpenAsync(episodesListLink);

            // Get all of the episodes
            var episodeLinks = document.Result.QuerySelectorAll("td")
                .Where(s => !s.TextContent.Contains("→") && !s.TextContent.Contains("←"))
                .Take(445) // 445 episodes. Generalizing it is possible, but unnecessary.
                .Select(c =>
                {
                    var episodeNameFull = c.QuerySelector("a").GetAttribute("href").Split('/').Last();
                    var episodeName = episodeNameFull.Substring(0, episodeNameFull.Length - "_(Episode)".Length);

                    return $"http://de.spongepedia.org/api.php?page=Episodenmitschrift:_{episodeName}&format=xml&action=parse&prop=text";
                });

            var caseDictionary = new Dictionary<string, string>(); // Will be used to show the correct case of each word
            var frequencyLists = GenerateFrequencyList(episodeLinks, caseDictionary);

            // Write to the file
            File.WriteAllLines("frequency.txt", frequencyLists.Select(kp => caseDictionary[kp.Key] + '\t' + kp.Value));
        }

        private static IEnumerable<KeyValuePair<string, int>> GenerateFrequencyList(IEnumerable<string> episodeLinks,
            IDictionary<string, string> caseDictionary)
        {
            var dictionary = new Dictionary<string, int>();
            var i = 0;

            foreach (var episodeLink in episodeLinks)
            {
                var xDoc = XDocument.Load(episodeLink);

                if (xDoc.Root == null) continue;

                var firstElement = xDoc.Root.Elements().First();

                if (firstElement.Name.LocalName.Equals("error"))
                {
                    Console.WriteLine($"Episode {episodeLink} doesn't exist");
                    continue;
                }

                // Get the HTML of the wiki page
                var allHtml = firstElement.Elements().First().Value;

                // Get the paragraphs only
                var paragraphs = new HtmlParser().Parse(allHtml).GetElementsByTagName("p");

                // Concatenate the paragraphs
                var text = paragraphs.Aggregate("", (current, paragraph) => current + (paragraph.TextContent += "\n"));

                // Get a list of words from the text
                var allWords = GetWords(text);

                foreach (var word in allWords)
                {
                    // If it's a number or has a length of 1, continue
                    if (int.TryParse(word, out var _) || word.Length == 1) continue;

                    if (dictionary.ContainsKey(word.ToLower()))
                    {
                        dictionary[word.ToLower()]++;
                    }
                    else
                    {
                        dictionary.Add(word.ToLower(), 1);
                        caseDictionary.Add(word.ToLower(), word);
                    }
                }

                Console.WriteLine("Finished episode " + ++i);
            }

            return dictionary.OrderByDescending(w => w.Value);
        }


        /*
         * The next two functions where taken from:
         * https://stackoverflow.com/questions/4970538/how-to-get-all-words-of-a-string-in-c
         */
        private static IEnumerable<string> GetWords(string input)
        {
            var matches = Regex.Matches(input, @"\b[\w']*\b");

            var words = from m in matches.Cast<Match>()
                        where !string.IsNullOrEmpty(m.Value)
                        select TrimSuffix(m.Value);

            return words.ToArray();
        }

        private static string TrimSuffix(string word)
        {
            var apostropheLocation = word.IndexOf('\'');

            if (apostropheLocation != -1)
            {
                word = word.Substring(0, apostropheLocation);
            }

            return word;
        }
    }
}
