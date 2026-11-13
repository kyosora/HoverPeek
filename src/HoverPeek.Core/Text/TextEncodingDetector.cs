using System.Text;

namespace HoverPeek.Core.Text;

/// <summary>
/// 判斷文字檔編碼。舊版用 File.ReadAllText(UTF8) 試讀，但 UTF-8 解碼器
/// 遇到非法位元組只會換成 U+FFFD、不會丟例外，所以 Big5 檔永遠被當成 UTF-8 顯示成亂碼。
/// 這裡改用嚴格解碼器判斷。
/// </summary>
public static class TextEncodingDetector
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static TextEncodingDetector()
    {
        // .NET Core 起 Big5、Shift-JIS 等 code page 預設不可用，要先註冊
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public sealed record Result(string Text, string EncodingName);

    /// <param name="truncated">
    /// true 表示 bytes 只是檔案開頭的一段，尾端可能切在多位元組字元中間；
    /// 會先修掉不完整的尾巴再判斷，避免 UTF-8 檔被誤判成 Big5。
    /// </param>
    public static Result Decode(ReadOnlySpan<byte> bytes, bool truncated = false)
    {
        if (truncated)
            bytes = bytes[..Utf8SafeLength(bytes)];

        if (TryDecodeWithBom(bytes, out var bomResult))
            return bomResult;

        if (TryStrict(StrictUtf8, bytes, out var utf8))
            return new Result(utf8, "UTF-8");

        if (TryStrict(Strict(Encoding.GetEncoding(950)), bytes, out var big5))
            return new Result(big5, "Big5");

        // 最後手段：系統 ANSI code page，非法字元以 � 呈現但至少能看
        var ansi = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        return new Result(ansi.GetString(bytes), ansi.WebName);
    }

    /// <summary>
    /// 回傳去掉尾端「不完整 UTF-8 序列」後的長度。最多往回看 3 個位元組
    /// （UTF-8 最長 4 位元組）。尾端完整、或根本不像 UTF-8 時回傳原長度。
    /// </summary>
    internal static int Utf8SafeLength(ReadOnlySpan<byte> bytes)
    {
        for (var back = 1; back <= Math.Min(3, bytes.Length); back++)
        {
            var b = bytes[^back];
            if ((b & 0b1100_0000) == 0b1000_0000)
                continue; // 延續位元組，繼續往回找開頭

            var expected = (b & 0b1110_0000) == 0b1100_0000 ? 2
                : (b & 0b1111_0000) == 0b1110_0000 ? 3
                : (b & 0b1111_1000) == 0b1111_0000 ? 4
                : 1;
            return expected > back ? bytes.Length - back : bytes.Length;
        }

        return bytes.Length;
    }

    private static bool TryDecodeWithBom(ReadOnlySpan<byte> bytes, out Result result)
    {
        (Encoding Enc, int Skip, string Name)? hit = bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => (Encoding.UTF8, 3, "UTF-8 BOM"),
            [0xFF, 0xFE, 0x00, 0x00, ..] => (Encoding.UTF32, 4, "UTF-32 LE"),
            [0xFF, 0xFE, ..] => (Encoding.Unicode, 2, "UTF-16 LE"),
            [0xFE, 0xFF, ..] => (Encoding.BigEndianUnicode, 2, "UTF-16 BE"),
            _ => null,
        };

        if (hit is { } h)
        {
            result = new Result(h.Enc.GetString(bytes[h.Skip..]), h.Name);
            return true;
        }

        result = null!;
        return false;
    }

    private static Encoding Strict(Encoding enc) =>
        Encoding.GetEncoding(enc.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    private static bool TryStrict(Encoding enc, ReadOnlySpan<byte> bytes, out string text)
    {
        try
        {
            text = enc.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }
}
