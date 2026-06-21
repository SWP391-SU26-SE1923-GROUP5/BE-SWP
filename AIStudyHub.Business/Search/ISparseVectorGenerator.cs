namespace AIStudyHub.Business.Search;

public interface ISparseVectorGenerator
{
    (List<uint> Indices, List<float> Values) GenerateSparseVector(string text);
}
