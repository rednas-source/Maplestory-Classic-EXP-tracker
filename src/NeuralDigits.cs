using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ViddyExp {
 public class Recognition {public string Text;public double Confidence;}
 public class NeuralDigits : IDisposable {
  InferenceSession session;string input;string[] chars;
  public NeuralDigits(){OrtEnv.Instance().DisableTelemetryEvents();using(var options=new SessionOptions()){options.IntraOpNumThreads=1;options.InterOpNumThreads=1;options.EnableCpuMemArena=false;session=new InferenceSession(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"models","digits.onnx"),options);}input=session.InputMetadata.Keys.First();chars=new[]{""}.Concat(session.ModelMetadata.CustomMetadataMap["character"].Split('\n')).Concat(new[]{" "}).ToArray();}
  public Recognition Read(Bitmap frame,Rectangle rect){
   rect=Rectangle.Intersect(rect,new Rectangle(Point.Empty,frame.Size));if(rect.Width<3||rect.Height<3)return new Recognition{Text=""};
   int width=Math.Max(320,(int)Math.Ceiling(48.0*rect.Width/rect.Height));width=Math.Min(1280,width);int resized=Math.Min(width,(int)Math.Ceiling(48.0*rect.Width/rect.Height));
   float[] data=new float[3*48*width];
   using(var b=new Bitmap(resized,48)){using(var g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.Bilinear;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(frame,new Rectangle(0,0,resized,48),rect,GraphicsUnit.Pixel);}var bits=b.LockBits(new Rectangle(0,0,resized,48),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var bytes=new byte[bits.Stride*48];Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);for(int y=0;y<48;y++)for(int x=0;x<resized;x++){int i=y*bits.Stride+x*4;data[y*width+x]=bytes[i]/127.5f-1;data[48*width+y*width+x]=bytes[i+1]/127.5f-1;data[96*width+y*width+x]=bytes[i+2]/127.5f-1;}}finally{b.UnlockBits(bits);}}
   var tensor=new DenseTensor<float>(data,new[]{1,3,48,width});
   using(var result=session.Run(new[]{NamedOnnxValue.CreateFromTensor(input,tensor)})){
    var output=result.First().AsTensor<float>();var dimensions=output.Dimensions.ToArray();int steps=dimensions[1],classes=dimensions[2],previous=-1,count=0;double sum=0;var text=new StringBuilder();
    var probabilities=output.ToArray();for(int t=0;t<steps;t++){int best=0;float score=0;for(int c=0;c<classes;c++){float value=probabilities[t*classes+c];if(value>score){score=value;best=c;}}if(best!=0&&best!=previous&&best<chars.Length){text.Append(chars[best]);sum+=score;count++;}previous=best;}
    return new Recognition{Text=text.ToString(),Confidence=count==0?0:sum/count};
   }
  }
  public void Dispose(){session.Dispose();}
 }
}
