using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

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

		private readonly List<TAPModel> m_results = new List<TAPModel>();

		private readonly StreamReader m_streamReader;
		private readonly Timer m_pollTimer;
		public TestAnythingProtocolParser(Stream stream)
		{
			m_streamReader = new StreamReader(stream);
			m_pollTimer = new Timer(ProcessTAP, null,Timeout.Infinite, Timeout.Infinite);
		}

		public void Start()
		{
			m_pollTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
		}

		public void Stop()
		{
			m_pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
		}

		private void ProcessTAP(object state)
		{
			while (m_streamReader.Peek() >= 0)
			{
				ParseLine(m_streamReader.ReadLine());
			}
		}

		private void ParseLine(string line)
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
					m_results.Last().YAML = m_yamlContent;
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

				m_results.Add(new TAPModel
				{
					Status = status,
					Index = index,
					YAML = "",
					Description = matches.Groups["description"].Value,
					Directive = matches.Groups["directive"].Value,
				});
			}
		}

		public IEnumerable<TAPModel> GetResults() { return m_results; }
	}
}
