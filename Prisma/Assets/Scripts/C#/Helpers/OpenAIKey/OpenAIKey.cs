using System.Text;

public static class OpenAIKey
{
    // Reconstruct the key
    // This is not a secure solution, but the api key has a low max usage limit to prevent abuse in case it leaks.
    public static string Reconstruct()
    {
        int n = System.Math.Min(KeyPartA.R.Length, KeyPartB.M.Length);
        var buf = new byte[n];
        for (int i = 0; i < n; i++)
            buf[i] = (byte)(KeyPartA.R[i] ^ KeyPartB.M[i]);

        var key = Encoding.ASCII.GetString(buf);
        System.Array.Clear(buf, 0, buf.Length);
        return key;
    }
}