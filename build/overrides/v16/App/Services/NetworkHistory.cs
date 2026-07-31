namespace DeskCanvas.App.Services;

internal sealed class NetworkHistory
{
    private readonly double[] values; private int next; private int count;
    internal NetworkHistory(int capacity = 30) => values = new double[Math.Max(2,capacity)];
    internal void Add(double value){values[next]=Math.Max(0,double.IsFinite(value)?value:0);next=(next+1)%values.Length;count=Math.Min(values.Length,count+1);}
    internal IReadOnlyList<double> Values => Enumerable.Range(0,count).Select(i=>values[(next-count+i+values.Length)%values.Length]).ToArray();
    internal double Scale(double floor=1024) => Math.Max(floor, Values.DefaultIfEmpty(0).Max());
    internal double[] Normalized(double floor=1024) { var scale=Scale(floor); return Values.Select(value=>Math.Clamp(value/scale,0,1)).ToArray(); }
}
