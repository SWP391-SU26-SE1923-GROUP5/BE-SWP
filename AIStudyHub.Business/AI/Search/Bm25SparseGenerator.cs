using AIStudyHub.Business.Interfaces.AI.Search;
using System.Text.RegularExpressions;

namespace AIStudyHub.Business.AI.Search;

public class Bm25SparseGenerator : ISparseVectorGenerator
{
    public (List<uint> Indices, List<float> Values) GenerateSparseVector(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (new List<uint>(), new List<float>());

        var words = Regex.Matches(text.ToLowerInvariant(), @"\b\w+\b")
            .Select(m => m.Value)
            .Where(w => w.Length > 2) // Bỏ qua các từ quá ngắn
            .ToList();

        var frequencies = new Dictionary<uint, float>();
        foreach (var word in words)
        {
            uint hash = CalculateHash(word);
            if (frequencies.ContainsKey(hash))
                frequencies[hash] += 1.0f;
            else
                frequencies[hash] = 1.0f;
        }

        var indices = new List<uint>();
        var values = new List<float>();

        // Cần sort indices tăng dần theo yêu cầu của Qdrant (Optional nhưng Best Practice)
        foreach (var kvp in frequencies.OrderBy(k => k.Key))
        {
            indices.Add(kvp.Key);
            // Áp dụng sub-linear TF để tránh việc nhồi nhét từ khóa làm sai lệch điểm số (TF = 1 + log(tf))
            values.Add((float)(1.0 + Math.Log(kvp.Value)));
        }

        return (indices, values);
    }

    private uint CalculateHash(string word)
    {
        // FNV-1a hash 32-bit (Phù hợp để map string thành integer index cho Sparse Vector)
        uint hash = 2166136261;
        foreach (char c in word)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }
}
