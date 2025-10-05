#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Security.Cryptography;
using System.Text;

public static class GenerateOpenAIKeyParts
{
    [MenuItem("Tools/Generate OpenAI Key Parts")]
    public static void Run()
    {
        const string key = "";

        // Keys are ASCII, but use UTF8 to be safe
        byte[] K = Encoding.UTF8.GetBytes(key);
        byte[] R = new byte[K.Length];   // random mask
        byte[] M = new byte[K.Length];   // masked = K ^ R

        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(R);

        for (int i = 0; i < K.Length; i++) M[i] = (byte)(K[i] ^ R[i]);

        // Verify reconstruction
        var recon = new byte[K.Length];
        for (int i = 0; i < K.Length; i++) recon[i] = (byte)(R[i] ^ M[i]);
        string reconStr = Encoding.UTF8.GetString(recon);
        if (reconStr != key)
        {
            Debug.LogError("Reconstruction FAILED. Length mismatch or copy bug.");
            return;
        }

        string ToInitializer(byte[] arr)
        {
            var sb = new StringBuilder("new byte[] { ");
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append(arr[i]);
                if (i != arr.Length - 1) sb.Append(", ");
            }
            sb.Append(" };");
            return sb.ToString();
        }

        Debug.Log("Key length: " + K.Length);
        Debug.Log("R (paste into KeyPartA.R):\n" + ToInitializer(R));
        Debug.Log("M (paste into KeyPartB.M):\n" + ToInitializer(M));
    }
}
#endif