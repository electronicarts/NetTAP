using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NetTAP
{
	public enum TestResult
	{
		NotOk,
		Ok,
		Skipped
	}

	public class TAPModel
	{
		public TestResult Status { get; set; }
		public uint Index { get; set; }
		public string Description { get; set; }
		public string Directive { get; set; }
		public string YAML { get; set; }
	}

	public class TestAnythingProtocolParser
	{
		private static readonly Regex s_testInformation = new Regex(@"(?<status>^(not )?ok\b)\s*(?<index>[0-9]*)\s*-\s*(?<description>[\w\s\.]*\b)\s*#?\s*(?<directive>[\w\s]*\b)?");
		private static readonly Regex s_yamlStartBlock = new Regex(@"(^\s)?---");
		private static readonly Regex s_yamlEndBlock = new Regex(@"(^\s)?\.\.\.");

		private bool m_parsingYaml;
		private string m_yamlContent = String.Empty;

		

		private readonly StreamReader m_streamReader;
		private Task m_parseTask;
		public TestAnythingProtocolParser(Stream stream)
		{
			m_streamReader = new StreamReader(stream, Encoding.UTF8);
		}

		public IEnumerable<TAPModel> Parse()
		{
			var results = new List<TAPModel>();

			var line = m_streamReader.ReadLine();
			while (line != null)
			{
				ParseLine(line, results);
				line = m_streamReader.ReadLine();
			}

			return results;
		}

		public Task<IEnumerable<TAPModel>> ParseAsync()
		{
			return Task.Run(() => Parse());
		}

		private void ParseLine(string line, List<TAPModel> results)
		{
			if (s_yamlStartBlock.IsMatch(line))
			{
				m_parsingYaml = true;
				m_yamlContent = "";
				return;
			}

			if (m_parsingYaml)
			{
				if (s_yamlEndBlock.IsMatch(line))
				{
					m_parsingYaml = false;
					results.Last().YAML = m_yamlContent;
					return;
				}

				m_yamlContent += line;
				return;
			}

			if (s_testInformation.IsMatch(line))
			{
				var matches = s_testInformation.Match(line);
				var status = TestResult.NotOk;
				if (matches.Groups["status"].Value.ToLower() == "ok")
				{
					status = TestResult.Ok;
				}

				uint index;
				uint.TryParse(matches.Groups["index"].Value, out index);

				results.Add(new TAPModel
				{
					Status = status,
					Index = index,
					YAML = "",
					Description = matches.Groups["description"].Value,
					Directive = matches.Groups["directive"].Value,
				});
			}
		}
	}
}
