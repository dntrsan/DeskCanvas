namespace DeskCanvas.App.Services;

/// <summary>Converts a frame to a frequency shape.  Absolute loudness is deliberately discarded.</summary>
internal static class SpectrumRelativeMath
{
 internal static double[] NormalizeDb(IReadOnlyList<double> db){var result=new double[db.Count];if(db.Count==0)return result;var strongest=db.Where(double.IsFinite).DefaultIfEmpty(double.NegativeInfinity).Max();if(!double.IsFinite(strongest)||strongest<-110)return result;for(var i=0;i<result.Length;i++)result[i]=double.IsFinite(db[i])?Math.Clamp((db[i]-strongest+36d)/36d,0,1):0;return result;}
 internal static double[] FromLinear(IReadOnlyList<double> values){if(values.Count==0)return [];var peak=values.Where(double.IsFinite).DefaultIfEmpty(0).Max();if(peak<1e-5)return new double[values.Count];var db=values.Select(x=>20*Math.Log10(Math.Max(x,1e-12))).ToArray();return NormalizeDb(db);}
}
