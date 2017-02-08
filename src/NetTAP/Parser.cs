using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;


namespace NetTAP
{
	public enum TestResult
	{
		NotOk,
		Ok,
	}

	enum ParseResult
	{
		TestResult,
		Error,
		Version,
		YamlStart,
		YamlEnd,
		YamlContent,
		TestPlan
	}

	public class TestPlan
	{
		public string Directive { get; set; }
		public uint FirstTestIndex { get; set; }
		public uint LastTestIndex { get; set; }
		public bool Todo { get; set; }
		public bool Skipped { get; set; }
	}

	public class TestSession
	{
		public uint TAPVersion { get; set; }
		public TestPlan TestPlan { get; set; }
		public IEnumerable<TestLine> Tests { get; set; }
	}

	public class TestLine
	{
		public TestResult Status { get; set; }
		public uint Index { get; set; }
		public string Description { get; set; }
		public string Directive { get; set; }
		public dynamic YAML { get; set; }
		public string YAMLError { get; set; }
		public bool Todo { get; set; }
		public bool Skipped { get; set; }
	}

	public class TestAnythingProtocolParser
	{
		private static readonly Regex s_testInformation =
			new Regex(
				@"(?<status>^(not )?ok\b)\s*(?<index>[0-9]*)\s*-?\s*(?<description>[\S\s-[#]]*\b)\s*#?\s*(?<directive>[\S\s]*\b)?");

		private static readonly Regex s_yamlStartBlock = new Regex(@"(^\s)?---");
		private static readonly Regex s_yamlEndBlock = new Regex(@"(^\s)?\.\.\.");
		private static readonly Regex s_tapVersion = new Regex(@"TAP\s+version\s+(?<version>\d*)");
		private static readonly Regex s_testPlan = new Regex(@"(?<firstplan>\d*)\.\.(?<lastplan>\d*)\s*#?\s*(?<directive>[\S\s]*\b)?");
		private static readonly Regex s_skippedDirective = new Regex(@"(?i)skip\S*\s+(?<reason>[\S\s]*)");
		private static readonly Regex s_todoDirective = new Regex(@"(?i)todo\S*\s+(?<reason>[\S\s]*)");

		private readonly StreamReader m_streamReader;
		private readonly Deserializer m_deserializer = new Deserializer();

		public TestAnythingProtocolParser(Stream stream)
		{
			m_streamReader = new StreamReader(stream, Encoding.UTF8);
		}

		public TestSession Parse()
		{
			string yamlContent = String.Empty;
			bool parsingYaml = false;

			TestPlan testPlan = new TestPlan();
			var results = new List<TestLine>();
			uint tapVersion = 0;

			var line = m_streamReader.ReadLine();
			while (line != null)
			{
				var parseResult = ParseLine(line, parsingYaml);

				switch (parseResult)
				{
					case ParseResult.TestResult:
						results.Add(ParseTestResult(line, (uint)results.Count + 1));
						break;
					case ParseResult.Error:
						break;
					case ParseResult.Version:
						tapVersion = ParseTAPVersion(line);
						break;
					case ParseResult.YamlStart:
						parsingYaml = true;
						yamlContent = "";
						break;
					case ParseResult.YamlEnd:
						parsingYaml = false;
						using (TextReader tr = new StringReader(yamlContent))
							results.Last().YAML = m_deserializer.Deserialize(tr);
						break;
					case ParseResult.YamlContent:
						yamlContent += line + "\n";
						break;
					case ParseResult.TestPlan:
						testPlan = ParseTestPlan(line);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				line = m_streamReader.ReadLine();
			}

			int testCount = (int)testPlan.LastTestIndex - (int)testPlan.FirstTestIndex + 1;

			if (results.Count < testCount)
			{
				for (int i = results.Count+1; i <= testCount; i++)
				{
					results.Add(new TestLine
					{
						Description = "",
						Directive = "",
						Index = (uint)i,
						Status = TestResult.NotOk,

					});
				}
			}

			return new TestSession
			{
				TAPVersion = tapVersion,
				TestPlan = testPlan,
				Tests = results
			};
		}

		public async Task<TestSession> ParseAsync()
		{
			var t = Task.Run(() => Parse());
			await t;
			return t.Result;
		}

		private static TestPlan ParseTestPlan(string line)
		{
			var testPlanMatch = s_testPlan.Match(line);
			uint firstTestIndex;
			uint lastTestIndex;
			uint.TryParse(testPlanMatch.Groups["firstplan"].Value, out firstTestIndex);
			uint.TryParse(testPlanMatch.Groups["lastplan"].Value, out lastTestIndex);

			string directive = testPlanMatch.Groups["directive"].Value;

			var skipMatch = s_skippedDirective.Match(directive);
			var todoMatch = s_todoDirective.Match(directive);

			if (skipMatch.Success)
			{
				directive = skipMatch.Groups["directive"].Value;
			}
			else if (todoMatch.Success)
			{
				directive = todoMatch.Groups["directive"].Value;
			}

			return new TestPlan
			{
				Directive = directive,
				FirstTestIndex = firstTestIndex,
				LastTestIndex = lastTestIndex,
				Todo = todoMatch.Success,
				Skipped = skipMatch.Success
			};
		}

		private uint ParseTAPVersion(string line)
		{
			var versionMatch = s_tapVersion.Match(line);
			uint version;
			uint.TryParse(versionMatch.Groups["version"].Value, out version);

			if (version > 13)
			{
				// Log some kind of warning as we most probably
				// don't support all features
			}

			return version;
		}

		private TestLine ParseTestResult(string line, uint currentIndex)
		{
			var matches = s_testInformation.Match(line);
			var status = TestResult.NotOk;
			if (matches.Groups["status"].Value.ToLower() == "ok")
			{
				status = TestResult.Ok;
			}

			uint index;
			if (!uint.TryParse(matches.Groups["index"].Value, out index))
				index = currentIndex;

			string directive = matches.Groups["directive"].Value;

			var skipMatch = s_skippedDirective.Match(directive);
			var todoMatch = s_todoDirective.Match(directive);

			if (skipMatch.Success)
				directive = skipMatch.Groups["reason"].Value;
			else if (todoMatch.Success)
				directive = todoMatch.Groups["reason"].Value;

			return new TestLine
			{
				Status = status,
				Index = index,
				YAML = "",
				Description = matches.Groups["description"].Value,
				Directive = directive,
				Skipped = skipMatch.Success,
				Todo = todoMatch.Success
			};
		}

		private ParseResult ParseLine(string line, bool yaml)
		{
			if (yaml)
			{
				// YAML End
				if (s_yamlEndBlock.IsMatch(line))
					return ParseResult.YamlEnd;

				return ParseResult.YamlContent;
			}

			// YAML Start
			if (s_yamlStartBlock.IsMatch(line))
				return ParseResult.YamlStart;

			// TAP Version
			if (s_tapVersion.IsMatch(line))
			{
				return ParseResult.Version;
			}

			// Test Plan
			if (s_testPlan.IsMatch(line))
			{
				return ParseResult.TestPlan;
			}

			// Test Line
			if (s_testInformation.IsMatch(line))
			{
				return ParseResult.TestResult;
			}

			return ParseResult.Error;
		}
	}
}
