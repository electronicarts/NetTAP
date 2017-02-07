using System.IO;
using System.Linq;
using Xunit;
using NetTAP;

namespace NetTAP.Tests
{
	public class Tests
	{
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
			var results = parser.Parse().ToList();

			Assert.True(results.Count == 4, "Expected count is 4");

			var firstTest = results.First();
			Assert.True(firstTest.Description == "Input file opened");
		}
	}
}
