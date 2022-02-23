using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace sankaku_tag_abbreviation_parser
{
	class Program
	{
		static void Main(string[] args)
		{
			//quick thrown-together parser to get a one-off job done.

			using (TextReader rdr = new StreamReader($@"D:\sankakuAbbreviationHtml.txt"))
			{
				string htmlLine;

				//assemble lines from text file
				List<string> fileExtract = new List<string>();
				while ((htmlLine = rdr.ReadLine()) != null)
				{
					fileExtract.Add(htmlLine);
				}

				//Extract only the lines we need
				List<List<string>> extractedAbbreviations = new List<List<string>>();
				List<string> abbrLineExtract = new List<string>();
				bool inTableRow = false;
				foreach(var line in fileExtract)
				{
					//parse out individual html blocks and add to extract list
					if (line.Trim().Equals("<tr class=\"\">"))
					{
						inTableRow = true;
					}

					if (inTableRow)
					{
						if (line.Trim().Equals("</tr>"))
						{
							inTableRow = false;
							extractedAbbreviations.Add(abbrLineExtract.ToList());
							abbrLineExtract.Clear();
							continue;
						}
						
						if (line.StartsWith("<td class=\"tag"))
						{
							abbrLineExtract.Add(line);
						}
					}
				}

				//parse the data from the extracted blocks
				List<Abbreviation> abbreviations = new List<Abbreviation>();
				foreach(var abbreviation in extractedAbbreviations)
				{
					Abbreviation parsedAbbreviation = new Abbreviation();
					var lineNo = 0;
					foreach (var line in abbreviation)
					{
						//each one of these has two html lines related to the tags.
						//first tag line -> corresponds to the short form, aka alias antecedent
						if (lineNo == 0)
						{
							string shortFormRx = "tags=(.+?)\"";

							Match shortformMatch = Regex.Match(line, shortFormRx, RegexOptions.IgnoreCase);

							if (shortformMatch.Success)
							{
								var shortform = HttpUtility.UrlDecode(shortformMatch.Groups[1].Value);
								parsedAbbreviation.shortform = shortform;
							}
							else
							{
								Console.WriteLine($@"something fucked up with: {line}");
							}
							lineNo = 1;
							continue;
						}

						//second tag line -> corresponds to the tag type and long form, aka alias consequent
						if (lineNo == 1)
						{
							string tagTypeRx = "tag-type-([a-zA-Z]+)?\"";
							string longformRx = "tags=(.+?)\"";
							Match tagTypeMatch = Regex.Match(line, tagTypeRx, RegexOptions.IgnoreCase);
							Match longformMatch = Regex.Match(line, longformRx, RegexOptions.IgnoreCase);

							if (tagTypeMatch.Success && longformMatch.Success)
							{
								var tagType = HttpUtility.UrlDecode(tagTypeMatch.Groups[1].Value);
								var longform = HttpUtility.UrlDecode(longformMatch.Groups[1].Value);
								parsedAbbreviation.tagType = tagType;
								parsedAbbreviation.longform = longform;

								abbreviations.Add(parsedAbbreviation);
							}
							else
							{
								Console.WriteLine($@"something fucked up with: {line}");
							}
							lineNo = 0;
							continue;
						}
					}
				}

				var filteredList = FilterAbbreviations(abbreviations);
				var sortedAbbreviations = filteredList.OrderBy(x => x.tagType).ThenBy(x => x.longform);

				foreach(var abbreviation in sortedAbbreviations)
				{
					//I'm copying the console output then replacing ||| with the excel new column character in np++ since it's faster.
					//doing any other remaining formatting in np++ as well. Not worth the time to write it into the code here.
					Console.WriteLine($@"{abbreviation.shortform}|||{abbreviation.longform}|||{abbreviation.tagType}");
				}
			}

		}

		
		private static List<Abbreviation> FilterAbbreviations(List<Abbreviation> abbreviations)
		{
			//there are a lot of fandom shortenings/nicknames that get filtered out here.
			//we really only want tags that start with a slash
			abbreviations.RemoveAll(x => !x.shortform.StartsWith("/"));
			var filteredList = abbreviations.ToList();

			foreach(var abbreviation in abbreviations)
			{
				//if there are multiple shortforms for a single longform
				//combine the shortforms into a single string so that there is a 1:1 ratio
				if(abbreviations.Where(x => x.longform == abbreviation.longform).Count() > 1)
				{
					var replacement = new Abbreviation();

					var duplicates = abbreviations.Where(x => x.longform == abbreviation.longform);
					StringBuilder combinedShortform = new StringBuilder();
					foreach (var duplicate in duplicates)
					{
						combinedShortform.Append(duplicate.shortform);
						if (duplicate != duplicates.Last())
						{
							combinedShortform.Append(" | ");
						}
					}

					replacement.tagType = abbreviation.tagType;
					replacement.longform = abbreviation.longform;
					replacement.shortform = combinedShortform.ToString();

					filteredList.RemoveAll(x => x.longform == abbreviation.longform);
					filteredList.Add(replacement);
				}
			}

			return filteredList;
		}
	}

	public class Abbreviation
	{
		public string shortform { get; set; }
		public string longform { get; set; }
		public string tagType { get; set; }
	}
}
