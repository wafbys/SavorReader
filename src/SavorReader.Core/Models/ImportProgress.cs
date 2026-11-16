namespace SavorReader.Core.Models;

public sealed record ImportProgress(int Completed, int Total, string CurrentFile)
{
    public double Fraction => Total <= 0 ? 0 : (double)Completed / Total;
}
