using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace NetTAP.Tests
{
	public class Tests
	{
		public class MockAsyncStream : Stream
		{
			private byte[] m_content;
			private Random m_random;
			private int m_position = 0;

			public MockAsyncStream(string content)
			{
				m_random = new Random(1234); // "Random"
				m_content = Encoding.UTF8.GetBytes(content);
			}

			public override void Flush()
			{
				throw new System.NotImplementedException();
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;

			public override long Length
			{
				get { throw new System.NotImplementedException(); }
			}

			public override long Position
			{
				get
				{
					throw new System.NotImplementedException();
				}
				set
				{
					throw new System.NotImplementedException();
				}
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				throw new System.NotImplementedException();
			}

			public override void SetLength(long value)
			{
				throw new System.NotImplementedException();
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new System.NotImplementedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				if (m_position == m_content.Length)
					return 0;

				int bytesToRead = m_random.Next(1, count);
				bytesToRead = Math.Min(bytesToRead, m_content.Length - m_position);

				int recieveTime = m_random.Next(1, 100);
				Thread.Sleep(recieveTime);

				Buffer.BlockCopy(m_content, m_position, buffer, offset, bytesToRead);
				m_position += bytesToRead;
				return bytesToRead;
			}
		}

		public MemoryStream CreateMemoryStream(string content)
		{
			var stream = new MemoryStream();
			var streamWriter = new StreamWriter(stream);
			streamWriter.Write(content);
			streamWriter.Flush();
			stream.Position = 0;

			return stream;
		}

		[Fact]
		public void ParsesBasicFileContent()
		{
			var tapContent = "TAP version 13\r\n" +
								"1..4\r\n" +
								"ok 1 - Input file opened\r\n" +
								"not ok 2 - First line of the input valid\r\n" +
								"  ---\r\n" +
								"  message: \'First line invalid\'\r\n" +
								"  severity: fail\r\n" +
								"  data:\r\n" +
								"    got: \'Flirble\'\r\n" +
								"    expect: \'Fnible\'\r\n" +
								"  ...\r\n" +
								"ok 3 - Read the rest of the file\r\n" +
								"not ok 4 - Summarized correctly # TODO Not written yet\r\n" +
								"  ---\r\n" +
								"  message: \"Can\'t make summary yet\"\r\n" +
								"  severity: todo\r\n" +
								"  ...";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.Equal(4, results.Count);

			var firstTest = results.First();
			Assert.True(firstTest.Description == "Input file opened");
		}

		[Fact]
		public void ParsesAsyncStream()
		{
			var tapContent = "TAP version 13\r\n" +
					"1..4\r\n" +
					"ok 1 - Input file opened\r\n" +
					"not ok 2 - First line of the input valid\r\n" +
					"  ---\r\n" +
					"  message: \'First line invalid\'\r\n" +
					"  severity: fail\r\n" +
					"  data:\r\n" +
					"    got: \'Flirble\'\r\n" +
					"    expect: \'Fnible\'\r\n" +
					"  ...\r\n" +
					"ok 3 - Read the rest of the file\r\n" +
					"not ok 4 - Summarized correctly # TODO Not written yet\r\n" +
					"  ---\r\n" +
					"  message: \"Can\'t make summary yet\"\r\n" +
					"  severity: todo\r\n" +
					"  ...";

			var parser = new TAPParser(new MockAsyncStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.True(results.Count == 4, "Expected count is 4");

			var fourthResult = results[3];
			Assert.Equal("Summarized correctly", fourthResult.Description);
			Assert.Equal("Not written yet", fourthResult.Directive);
			Assert.False(String.IsNullOrEmpty(fourthResult.YAML["message"]), "Expected to contain YAML content.");
			Assert.Equal(fourthResult.YAML["message"], "Can't make summary yet");
		}

		[Fact]
		public void ParsesMissingTest()
		{
			var tapContent = "TAP version 13\n" +
					"1..3\n" +
					"ok 1\n" +
					"not ok 2\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.True(results.Count == 3, "Expected count to be 3 even though only two tests were reported");
			var thirdResult = results[2];

			Assert.True(thirdResult.Status == TestResult.NotOk);
		}

		[Fact]
		public void ParseTodoDirective()
		{
			var tapContent = "TAP version 13\n" +
					"1..2\n" +
					"ok 1\n" +
					"not ok 2 # TODO sune must fix\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			var result = results[1];

			Assert.True(result.Todo);
			Assert.Equal(TestResult.NotOk, result.Status);
			Assert.Empty(result.Description);
			Assert.Equal("sune must fix", result.Directive);
		}

		[Fact]
		public void ParsesMissingIndex()
		{
			var tapContent = "TAP version 13\n" +
					"1..3\n" +
					"ok - Sune\n" +
					"not ok 2\n" +
					"not ok\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.Equal(1u, results[0].Index);
			Assert.Equal(3u, results[2].Index);
		}

		[Fact]
		public void ParsesMissingDescAndIndexWithDirective()
		{
			var tapContent = "TAP version 13\n" +
							"1..2\n" +
							"ok # TODO Sune\n" +
							"not ok # Skip Sune\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.Equal(1u, results[0].Index);
			Assert.Equal(2u, results[1].Index);

			Assert.True(results[0].Todo);
			Assert.Equal("Sune", results[0].Directive);

			Assert.True(results[1].Skipped);
			Assert.Equal("Sune", results[1].Directive);
		}

		[Fact]
		public void ParsesTestPlanPostTests()
		{
			var tapContent = "TAP version 13\n" +
				"ok - Sayan! # TODO Sune\n" +
				"not ok - ÄÅÖ # Skip Sune\n" +
				"1..3\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.Equal(3, results.Count);

			Assert.Equal(1u, results[0].Index);
			Assert.Equal(2u, results[1].Index);
			Assert.Equal(3u, results[2].Index);

			Assert.True(results[0].Todo);
			Assert.Equal("Sayan!", results[0].Description);

			Assert.True(results[1].Skipped);
			Assert.Equal("ÄÅÖ", results[1].Description);

			Assert.Equal(results[2].Status, TestResult.NotOk);
			Assert.Equal("", results[2].Description);
		}

		[Fact]
		public void ParsesSkippedTestPlan()
		{
			var tapContent =  "1..0 # Skipped: WWW::Wok not installed";
			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse();
			var tests = results.Tests.ToList();

			Assert.Equal(0, tests.Count);
			Assert.Equal(0u, results.TestPlan.LastTestIndex);
			Assert.Equal(1u, results.TestPlan.FirstTestIndex);
			Assert.Equal("WWW::Wok not installed", results.TestPlan.Directive);
			Assert.True(results.TestPlan.Skipped);
		}

		[Fact]
		public void ParsesTestsNoPlan()
		{
			var tapContent = "TAP version 13\n" +
				"ok # TODO Sune\n" +
				"not ok # Skip Sune\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			Assert.Equal(1u, results[0].Index);
			Assert.Equal(2u, results[1].Index);

			Assert.True(results[0].Todo);
			Assert.Equal("Sune", results[0].Directive);

			Assert.True(results[1].Skipped);
			Assert.Equal("Sune", results[1].Directive);
		}

		[Fact]
		public void ParsesTAPVersion()
		{
			var tapContent = "TAP version 11\n" +
					"ok # TODO Sune\n" +
					"not ok # Skip Sune\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse();

			Assert.Equal(11u, results.TAPVersion);
		}

		[Fact]
		public void ParsesTooHighTAPVersion()
		{
			var tapContent = "TAP version 100\n" +
					"ok # TODO Sune\n" +
					"not ok # Skip Sune\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			Assert.Throws<TAPParserException>(() => parser.Parse());
		}

		[Fact]
		public void ParseTestSessionDiagnostics()
		{
			var tapContent = "TAP version 13\n" +
					"# Some diagnostics\n" +
					"# Some more diagnostics\n" +
					"ok # TODO Sune\n" +
					"# Some test diagnostic\n" +
					"not ok # Skip Sune\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse();

			Assert.Equal(2, results.DiagnosticMessages.Count);
			Assert.Equal("Some diagnostics", results.DiagnosticMessages[0]);
			Assert.Equal("Some more diagnostics", results.DiagnosticMessages[1]);
		}

		[Fact]
		public void ParseTestDiagnostics()
		{
			var tapContent = "TAP version 13\n" +
					"ok # TODO Sune\n" +
					"# Some test diagnostic\n" +
					"# Some Sune\n" +
					"not ok # Skip Sune\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse();
			var tests = results.Tests.ToList();

			Assert.Equal(0, results.DiagnosticMessages.Count);

			Assert.Equal(2, tests[0].DiagnosticMessages.Count);
			Assert.Equal("Some test diagnostic", tests[0].DiagnosticMessages[0]);
			Assert.Equal("Some Sune", tests[0].DiagnosticMessages[1]);

			Assert.Equal(0, tests[1].DiagnosticMessages.Count);
		}

		[Fact]
		public void ParsesBailOut()
		{
			var tapContent = "TAP version 13\n" +
			"ok # TODO Sune\n" +
			"# Some test diagnostic\n" +
			"# Some Sune\n" +
			"Bail Out! Det ballar ur!\n";

			var parser = new TAPParser(CreateMemoryStream(tapContent));
			var results = parser.Parse();

			Assert.True(results.BailedOut);
			Assert.Equal("Det ballar ur!", results.BailOutMessage);
		}

		[Fact]
		public void RecievesParserVersionAction()
		{
			var tapContent = "TAP version 13\n";
			uint version = 0;
			var ev = new ManualResetEvent(false);
			var parser = new TAPParser(CreateMemoryStream(tapContent));
			parser.OnVersion += u =>
			{
				version = u;
				ev.Set();
			};

			var t = parser.ParseAsync();
			Assert.True(ev.WaitOne());
			Assert.Equal(13u, version);
			t.Wait();
		}

		[Fact]
		public void RecievesParserTestPlanAction()
		{
			var tapContent = "TAP version 13\n" +
							"1..5\n";

			uint firstIndex = 0;
			uint lastIndex = 0;

			var ev = new ManualResetEvent(false);
			var parser = new TAPParser(CreateMemoryStream(tapContent));
			parser.OnTestPlan += u =>
			{
				firstIndex = u.FirstTestIndex;
				lastIndex = u.LastTestIndex;
				ev.Set();
			};

			var t = parser.ParseAsync();
			Assert.True(ev.WaitOne());
			Assert.Equal(1u, firstIndex);
			Assert.Equal(5u, lastIndex);
			t.Wait();
		}

		[Fact]
		public void RecievesParserDiagnosticAction()
		{
			var tapContent = "TAP version 13\n" +
						"# Some diagnostics\n" +
						"# Some more diagnostics\n" +
						"ok # TODO Sune\n" +
						"# Some test diagnostic\n" +
						"not ok # Skip Sune\n";

			var diagnosticMessages = new List<string>();

			var ev = new CountdownEvent(2);
			var parser = new TAPParser(CreateMemoryStream(tapContent));
			parser.OnDiagnostic += message =>
			{
				diagnosticMessages.Add(message);
				ev.Signal();
			};

			var t = parser.ParseAsync();
			ev.Wait();
			Assert.Equal(2, diagnosticMessages.Count);
			Assert.Equal("Some diagnostics", diagnosticMessages[0]);
			Assert.Equal("Some more diagnostics", diagnosticMessages[1]);
			t.Wait();
		}

		[Fact]
		public void RecievesParserTestLineDiagnosticAction()
		{
			var tapContent = "TAP version 13\n" +
						"# Some diagnostics\n" +
						"# Some more diagnostics\n" +
						"ok # TODO Sune\n" +
						"# Some test diagnostic\n";

			string diagnosticsMessage = string.Empty;
			int recievedCount = 0;
			TestLine testLine = null;

			var ev = new ManualResetEvent(false);
			var parser = new TAPParser(CreateMemoryStream(tapContent));
			parser.OnTestResultDiagnostic += (tl, message) =>
			{
				recievedCount++;
				diagnosticsMessage = message;
				testLine = tl;
				ev.Set();
			};

			var t = parser.ParseAsync();
			Assert.True(ev.WaitOne());
			Assert.Equal(1, recievedCount);
			Assert.Equal("Some test diagnostic", diagnosticsMessage);
			Assert.Equal("Sune", testLine.Directive);
			t.Wait();
		}

		[Fact]
		public void RecievesParserTestLineAction()
		{
			var tapContent = "TAP version 13\n" +
					"1..3\n" +
					"ok - Sune\n" +
					"not ok 2 - Bune\n" +
					"not ok - Lune\n";

			var ev = new CountdownEvent(3);
			var parser = new TAPParser(CreateMemoryStream(tapContent));
			TestLine testLine = null;
			parser.OnTestResult += line =>
			{
				testLine = line;
				ev.Signal();
			};

			var t = parser.ParseAsync();
			ev.Wait();
			
			Assert.Equal(0, ev.CurrentCount);
			Assert.NotNull(testLine);
			Assert.Equal("Lune", testLine.Description);
			t.Wait();
		}

		[Fact]
		public void RecievesParserErrorAction()
		{
			var tapContent = "asdsdagsfad #¤)=/=!(¤=!¤9";

			var ev = new ManualResetEvent(false);
			var parser = new TAPParser(CreateMemoryStream(tapContent));

			parser.OnError += exception =>
			{
				ev.Set();
			};
			
			var t = parser.ParseAsync();
			Assert.True(ev.WaitOne());
			Assert.Throws<AggregateException>(() => t.Wait());
		}

		[Fact]
		public void RecievesParserYamlAction()
		{
			var tapContent = "TAP version 13\r\n" +
						"1..4\r\n" +
						"ok 1 - Input file opened\r\n" +
						"not ok 2 - First line of the input valid\r\n" +
						"  ---\r\n" +
						"  message: \'First line invalid\'\r\n" +
						"  ...\r\n" +
						"ok 3 - Read the rest of the file\r\n";

			TestLine line = null;
			dynamic yaml = null;

			var parser = new TAPParser(new MockAsyncStream(tapContent));
			var ev = new ManualResetEvent(false);
			parser.OnYaml += (tl, o) =>
			{
				line = tl;
				yaml = o;
				ev.Set();
			};

			var t = parser.ParseAsync();
			Assert.True(ev.WaitOne());
			Assert.NotNull(yaml);
			Assert.NotNull(line);
			Assert.Equal(yaml, line.YAML);
			Assert.Equal("First line invalid", yaml["message"]);
			Assert.Equal("First line of the input valid", line.Description);
			t.Wait();
		}

		[Fact]
		public void RecievesParserBailOutAction()
		{
			var tapContent = "TAP version 13\r\n" +
						"1..4\r\n" +
						"ok 1 - Input file opened\r\n" +
						"not ok 2 - First line of the input valid\r\n" +
						"BAIL OUT! SUNE LEFT THE BUILDING!";

			var parser = new TAPParser(new MockAsyncStream(tapContent));
			var ev = new ManualResetEvent(false);
			string bailoutMessage = String.Empty;
			parser.OnBailout += s =>
			{
				bailoutMessage = s;
				ev.Set();
			};

			var t = parser.ParseAsync();
			Assert.True(ev.WaitOne());
			Assert.Equal("SUNE LEFT THE BUILDING!", bailoutMessage);
			var res = t.Result;
			Assert.True(res.BailedOut);
		}
	}
}
