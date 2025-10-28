namespace Utility;

public class FloatConverter
{
    public static byte[] FloatArrayToBytes(float[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        var bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] BytesToFloatArray(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length % sizeof(float) != 0)
            throw new ArgumentException("バイト列の長さが 4 の倍数ではありません。", nameof(bytes));

        var values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }
}
