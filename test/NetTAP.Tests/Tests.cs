using System;
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

			var parser = new TestAnythingProtocolParser(CreateMemoryStream(tapContent));
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

			var parser = new TestAnythingProtocolParser(new MockAsyncStream(tapContent));
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

			var parser = new TestAnythingProtocolParser(CreateMemoryStream(tapContent));
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

			var parser = new TestAnythingProtocolParser(CreateMemoryStream(tapContent));
			var results = parser.Parse().Tests.ToList();

			var result = results[1];

			Assert.True(result.Todo);
			Assert.Equal(TestResult.NotOk, result.Status);
			Assert.Empty(result.Description);
			Assert.Equal("sune must fix", result.Directive);
		}
	}
}
