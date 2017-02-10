// Copyright (C) Electronic Arts Inc. All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
		TestPlan,
		Diagnostic,
		BailOut
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
		public List<string> DiagnosticMessages { get; set; } = new List<string>();
		public bool BailedOut { get; set; }
		public string BailOutMessage { get; set; }
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
		public List<string> DiagnosticMessages { get; set; } = new List<string>();
	}

	public class TAPParserException : Exception
	{
		public TAPParserException(string message) : base(message)
		{
		}
	}

	public class TAPParser
	{
		private static readonly Regex s_testInformation =
			new Regex(
				@"(?<status>^(not )?ok\b)\s*(?<index>[0-9]*)\s*-?\s*(?<description>[\S\s-[#]]*)\s*#?\s*(?<directive>[\S\s]*)?");

		private static readonly Regex s_yamlStartBlock = new Regex(@"(^\s)?---");
		private static readonly Regex s_yamlEndBlock = new Regex(@"(^\s)?\.\.\.");
		private static readonly Regex s_tapVersion = new Regex(@"TAP\s+version\s+(?<version>\d*)");

		private static readonly Regex s_testPlan =
			new Regex(@"(?<firstplan>\d*)\.\.(?<lastplan>\d*)\s*#?\s*(?<directive>[\S\s]*\b)?");

		private static readonly Regex s_skippedDirective = new Regex(@"(?i)skip\S*\s+(?<reason>[\S\s]*)");
		private static readonly Regex s_todoDirective = new Regex(@"(?i)todo\S*\s+(?<reason>[\S\s]*)");
		private static readonly Regex s_diagnostics = new Regex(@"^\s*#(?<diagnostic>[\S\s]*)");
		private static readonly Regex s_bailout = new Regex(@"(?i)Bail Out!\s*(?<message>[\S\s]*)");

		private readonly Deserializer m_deserializer = new Deserializer();

		public event Action<TestLine> OnTestResult;
		public event Action<Exception> OnError;
		public event Action<uint> OnVersion;
		public event Action<TestLine, dynamic> OnYaml;
		public event Action<TestPlan> OnTestPlan;
		public event Action<TestLine, string> OnTestResultDiagnostic;
		public event Action<string> OnDiagnostic;
		public event Action<string> OnBailout;

		public TestSession Parse(Stream stream)
		{
			var streamReader = new StreamReader(stream, Encoding.UTF8);
			string yamlContent = String.Empty;
			bool parsingYaml = false;

			var testPlan = new TestPlan();
			var results = new List<TestLine>();
			uint tapVersion = 0;
			var sessionDiagnosticMessages = new List<string>();
			string bailoutMessage = String.Empty;
			bool bailedOut = false;

			var line = streamReader.ReadLine();
			while (line != null)
			{
				var parseResult = ParseLine(line, parsingYaml);

				switch (parseResult)
				{
					case ParseResult.TestResult:
						var result = ParseTestResult(line, (uint) results.Count + 1);
						results.Add(result);
						try
						{
							OnTestResult?.Invoke(result);
						}
						catch (Exception e)
						{
							SendError(e);
						}

						break;
					case ParseResult.Error:
						if (!string.IsNullOrEmpty(line))
						{
							var e = new TAPParserException($"TAP syntax error. Unrecognized line \"{line}\".");
							SendError(e);
						}
						break;
					case ParseResult.Version:
						var v = ParseTAPVersion(line);
						try
						{
							OnVersion?.Invoke(v);
						}
						catch (Exception e)
						{
							SendError(e);
						}

						tapVersion = v;
						break;
					case ParseResult.YamlStart:
						parsingYaml = true;
						yamlContent = "";
						break;
					case ParseResult.YamlEnd:
						parsingYaml = false;
						using (TextReader tr = new StringReader(yamlContent))
						{
							dynamic yaml = m_deserializer.Deserialize(tr);
							var testLine = results.Last();
							testLine.YAML = yaml;

							try
							{
								OnYaml?.Invoke(testLine, yaml);
							}
							catch (Exception e)
							{
								SendError(e);
							}
						}
						break;
					case ParseResult.YamlContent:
						yamlContent += line + "\n";
						break;
					case ParseResult.TestPlan:
						var tp = ParseTestPlan(line);
						
						try
						{
							OnTestPlan?.Invoke(tp);
						}
						catch (Exception e)
						{
							SendError(e);
						}
						testPlan = tp;
						break;
					case ParseResult.Diagnostic:
						string diagnostic = s_diagnostics.Match(line).Groups["diagnostic"].Value.Trim();
						if (results.Any())
						{
							var testLine = results.Last();
							testLine.DiagnosticMessages.Add(diagnostic);
							
							try
							{
								OnTestResultDiagnostic?.Invoke(testLine, diagnostic);
							}
							catch (Exception e)
							{
								SendError(e);
							}
						}
						else
						{
							sessionDiagnosticMessages.Add(diagnostic);
							
							try
							{
								OnDiagnostic?.Invoke(diagnostic);
							}
							catch (Exception e)
							{
								SendError(e);
							}
						}
						break;
					case ParseResult.BailOut:
						var message = s_bailout.Match(line).Groups["message"].Value.Trim();
						bailedOut = true;
						
						try
						{
							OnBailout?.Invoke(message);
						}
						catch (Exception e)
						{
							SendError(e);
						}
						bailoutMessage = message;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				line = streamReader.ReadLine();
			}

			int testCount = (int)testPlan.LastTestIndex - (int)testPlan.FirstTestIndex + 1;

			if (results.Count < testCount)
			{
				for (int i = results.Count+1; i <= testCount; i++)
				{
					var testLine = new TestLine
					{
						Description = "",
						Directive = "",
						Index = (uint)i,
						Status = TestResult.NotOk,

					};

					results.Add(testLine);
					
					try
					{
						OnTestResult?.Invoke(testLine);
					}
					catch (Exception e)
					{
						SendError(e);
					}
				}
			}

			return new TestSession
			{
				TAPVersion = tapVersion,
				TestPlan = testPlan,
				Tests = results,
				DiagnosticMessages = sessionDiagnosticMessages,
				BailOutMessage = bailoutMessage,
				BailedOut = bailedOut
			};
		}

		public async Task<TestSession> ParseAsync(Stream stream)
		{
			var t = Task.Run(() => Parse(stream));
			await t;
			return t.Result;
		}

		private void SendError(Exception e)
		{
			try
			{
				OnError?.Invoke(e);
			}
			catch (Exception) { }
		}

		private class ParsedDirective
		{
			public string Directive { get; set; }
			public bool Todo { get; set; }
			public bool Skipped { get; set; }
		}

		private static ParsedDirective ParseDirective(string input)
		{
			var skipMatch = s_skippedDirective.Match(input);
			var todoMatch = s_todoDirective.Match(input);
			string directive = input;

			if (skipMatch.Success)
			{
				directive = skipMatch.Groups["reason"].Value;
			}
			else if (todoMatch.Success)
			{
				directive = todoMatch.Groups["reason"].Value;
			}

			return new ParsedDirective
			{
				Skipped = skipMatch.Success,
				Todo = todoMatch.Success,
				Directive = directive.Trim()
			};
		}

		private static TestPlan ParseTestPlan(string line)
		{
			var testPlanMatch = s_testPlan.Match(line);
			uint firstTestIndex;
			uint lastTestIndex;
			uint.TryParse(testPlanMatch.Groups["firstplan"].Value, out firstTestIndex);
			uint.TryParse(testPlanMatch.Groups["lastplan"].Value, out lastTestIndex);
			var directive = ParseDirective(testPlanMatch.Groups["directive"].Value);

			return new TestPlan
			{
				Directive = directive.Directive,
				FirstTestIndex = firstTestIndex,
				LastTestIndex = lastTestIndex,
				Todo = directive.Todo,
				Skipped = directive.Skipped
			};
		}

		private uint ParseTAPVersion(string line)
		{
			var versionMatch = s_tapVersion.Match(line);
			uint version;
			uint.TryParse(versionMatch.Groups["version"].Value, out version);

			if (version > 13)
				throw new TAPParserException($"TAPParser does not support versions > 13. Was {version}.");

			return version;
		}

		private TestLine ParseTestResult(string line, uint currentIndex)
		{
			var matches = s_testInformation.Match(line);
			var status = TestResult.NotOk;
			if (matches.Groups["status"].Value.ToLower() == "ok")
				status = TestResult.Ok;

			uint index;
			if (!uint.TryParse(matches.Groups["index"].Value, out index))
				index = currentIndex;

			var directive = ParseDirective(matches.Groups["directive"].Value);

			return new TestLine
			{
				Status = status,
				Index = index,
				YAML = "",
				Description = matches.Groups["description"].Value.Trim(),
				Directive = directive.Directive,
				Skipped = directive.Skipped,
				Todo = directive.Todo
			};
		}

		private ParseResult ParseLine(string line, bool yaml)
		{
			// YAML End/Content
			if (yaml)
				return s_yamlEndBlock.IsMatch(line) ? ParseResult.YamlEnd : ParseResult.YamlContent;

			// YAML Start
			if (s_yamlStartBlock.IsMatch(line))
				return ParseResult.YamlStart;

			// TAP Version
			if (s_tapVersion.IsMatch(line))
				return ParseResult.Version;

			// Diagnostics
			if (s_diagnostics.IsMatch(line))
				return ParseResult.Diagnostic;

			// Test Plan
			if (s_testPlan.IsMatch(line))
				return ParseResult.TestPlan;

			// Test Line
			if (s_testInformation.IsMatch(line))
				return ParseResult.TestResult;

			if(s_bailout.IsMatch(line))
				return ParseResult.BailOut;

			return ParseResult.Error;
		}
	}
}
