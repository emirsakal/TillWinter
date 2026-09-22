using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>A farm carried between phones as text: it survives the trip whole, or it is refused.</summary>
    public class SaveTransferTests
    {
        private const string Json = "{\"SchemaVersion\":17,\"Coins\":1234.5,\"Plots\":[1,2,3]}";

        [Test]
        public void RoundTrip_ReturnsTheSameJson()
        {
            var code = SaveTransfer.Encode(Json);
            Assert.IsTrue(code.StartsWith(SaveTransfer.Prefix + "."), code);
            Assert.IsTrue(SaveTransfer.TryDecode(code, out var back));
            Assert.AreEqual(Json, back);
        }

        [Test]
        public void Decode_IgnoresWhitespaceAndLineBreaks()
        {
            var code = SaveTransfer.Encode(Json);
            var messy = "  " + code.Substring(0, 12) + "\r\n" + code.Substring(12) + "\n ";
            Assert.IsTrue(SaveTransfer.TryDecode(messy, out var back));
            Assert.AreEqual(Json, back);
        }

        [Test]
        public void Decode_RefusesATruncatedCode()
        {
            var code = SaveTransfer.Encode(Json);
            Assert.IsFalse(SaveTransfer.TryDecode(code.Substring(0, code.Length - 12), out _));
        }

        [Test]
        public void Decode_RefusesAnEditedPayload()
        {
            var code = SaveTransfer.Encode(Json);
            // Flip one payload character for another legal base64 one: the base64 still parses, the checksum does not.
            int i = code.Length - 6;
            var edited = code.Substring(0, i) + (code[i] == 'A' ? 'B' : 'A') + code.Substring(i + 1);
            Assert.IsFalse(SaveTransfer.TryDecode(edited, out _));
        }

        [Test]
        public void Decode_RefusesRubbish()
        {
            Assert.IsFalse(SaveTransfer.TryDecode("", out _));
            Assert.IsFalse(SaveTransfer.TryDecode(null, out _));
            Assert.IsFalse(SaveTransfer.TryDecode("hello", out _));
            Assert.IsFalse(SaveTransfer.TryDecode("TW2.12345678.AAAA", out _));
            Assert.IsFalse(SaveTransfer.TryDecode("TW1.12345678.not base64!", out _));
        }
    }
}
