using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Cairo;

/*
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;
*/

using System.Xml.Serialization;



namespace WordCloud
{
	[Serializable]
    public class WordCloud
    {
       
        //private Grid _layoutRoot;
        private int[] _mapping;
        private ImageSurface _source;
		private Context g;
		private byte[] pixelss;
		private int strides;
		private Random r;

		public int ActualWidth = 800;
		public int ActualHeight = 600;

        public WordCloud()
        {
			//throw new NotImplementedException("WordCloud(): not implemented!\n");
        }

		[XmlIgnore]
		public List<WordCloudEntry> Entries;

		public string fontname = "Droid Sans";

		public double LargestSizeWidthProportion = 1.0;

		public double MinFontSize = 10.0;
		public double MaxFontSize = 0.0;
		public double vratio = 0.3;

		private SvgSurface svgs = null;
		private Context svg = null;
		private PdfSurface pdfs = null;
		private Context pdf = null;
		[XmlIgnore]
		public string svgfn = null;
		[XmlIgnore]
		public string pdffn = null;

		public string colorstring = "#000000";

		public double CoverRatio = 0.0;
		public double FontMaxRatio = 6.0;

        //const int b = 0;
        //const int g = 1;
        //const int r = 2;
        //const int a = 3;
		const int bytes = 4;
/*
		[XmlIgnore]
		public FontFamily font;
*/
        public void InternalRegenerateCloud()
        {
			r = new Random();
			int id = 0;
            double minSize = Entries.Min(e => e.SizeValue);
            double maxSize = Entries.Max(e => e.SizeValue);
            double range = Math.Max(0.00001, maxSize - minSize);
   
			_source = new ImageSurface(Format.Argb32,ActualWidth,ActualHeight);
			pixelss = _source.Data;
			g = new Context(_source);
			g.Color = new Color(1.0,1.0,1.0);
			//g.Rectangle(0.0,0.0,ActualWidth,ActualHeight);
			g.Paint();

			g.SelectFontFace(fontname,FontSlant.Normal,FontWeight.Bold);
			g.SetFontSize(100.0);
			TextExtents te = g.TextExtents("x");
			double areaPerLetter = te.Width;

			strides = _source.Stride;
			int arraySize = (int)((ActualWidth / 4) + 2) * (int)((ActualHeight / 4) + 2);
            _mapping = new int[arraySize];
            for (int i = 0; i < arraySize; i++) _mapping[i] = -1;


			if(svgfn != null) {
				svgs = new SvgSurface(svgfn,ActualWidth,ActualHeight);
				svg = new Context(svgs);
				svg.SelectFontFace(fontname,FontSlant.Normal,FontWeight.Bold);
			}
			if(pdffn != null) {
				pdfs = new PdfSurface(pdffn,ActualWidth,ActualHeight);
				pdf = new Context(pdfs);
				pdf.SelectFontFace(fontname,FontSlant.Normal,FontWeight.Bold);
			}

			double fontMultiplier;

			if(CoverRatio > 0.0) {
				double totalsize = 0.0;
				MinFontSize = 24.0; // use big fonts for testing
				if(FontMaxRatio < 1.0 || FontMaxRatio > 500.0) FontMaxRatio = 6.0;
				MaxFontSize = FontMaxRatio*MinFontSize;
				fontMultiplier = (MaxFontSize - MinFontSize) / range;
				foreach(WordCloudEntry e in Entries) {
					float fontsize = (float)(((e.SizeValue - minSize) * fontMultiplier) + MinFontSize);
					g.SetFontSize(fontsize);
					TextExtents te1 = g.TextExtents(e.Word);
					totalsize += te1.Width*te1.Height;
				}
				double actualsize = ActualHeight*ActualWidth;
				double ratio1 = totalsize / actualsize; //this should be == CoverRatio
				//we assume that totalsize ~ MinFontSize^2 in this case
				MinFontSize = MinFontSize*Math.Sqrt(CoverRatio / ratio1);
				MaxFontSize = FontMaxRatio*MinFontSize;
				fontMultiplier = (MaxFontSize - MinFontSize) / range;
				LargestSizeWidthProportion = 0.9;
			}
			
            double targetWidth = Math.Max(ActualWidth, ActualHeight) * LargestSizeWidthProportion;
            WordCloudEntry od = Entries.OrderByDescending(e => (e.SizeValue - minSize) * e.Word.Length).First();
			double maxFontSize;
			if(MaxFontSize > 0.0 && CoverRatio <= 0.0) {
				maxFontSize = MaxFontSize;
				if(maxFontSize < MinFontSize) maxFontSize = MinFontSize*2.7;
			}
			else {
				g.SetFontSize(100.0);
				TextExtents te2 = g.TextExtents(od.Word);
				maxFontSize = 97.0*targetWidth / te2.Width;
				//maxFontSize = Math.Max(MinFontSize * 2.7, 100.0 / ((od.Word.Length * areaPerLetter) / targetWidth));
				if(maxFontSize > MinFontSize + 100) maxFontSize = MinFontSize + 100;
			}
			if(CoverRatio > 0.0) {
				if(maxFontSize < MaxFontSize) MaxFontSize = maxFontSize;
				fontMultiplier = (maxFontSize - MinFontSize) / range;
			}
			else fontMultiplier = (maxFontSize - MinFontSize) / range;


            var points = new[]
                             {
                                 new Point((int) (ActualWidth/2), (int) (ActualHeight/2)),
                                 new Point((int) (ActualWidth/4), (int) (ActualHeight/4)),
                                 new Point((int) (ActualWidth/4), (int) (3*ActualHeight/2)),
                                 new Point((int) (3*ActualWidth/4), (int) (ActualHeight/2)),
                                 new Point((int) (3*ActualWidth/4), (int) (3*ActualHeight/4))
                             };


            int currentPoint = 0;
            foreach (WordCloudEntry e in Entries.OrderByDescending(e => e.SizeValue))
            {
            again:
                double position = 0.0;
                Point centre = points[currentPoint];

                double angle = 0.0;
                if(vratio > 0.0)
                {
					if(r.NextDouble() < vratio) angle = 90.0;
                }
				float fontsize = (float)(((e.SizeValue - minSize) * fontMultiplier) + MinFontSize);
				ImageSurface bm;
			imgretry:
                bm = CreateImage(e.Word,fontsize,e.Color,angle);
				// test if it fits
				if(angle == 90.0) {
					if(bm.Height > ActualHeight) {
						if(ActualWidth > ActualHeight) {
							angle = 0.0;
							bm.Destroy();
							bm = null;
							goto imgretry;
						}
						// crop the end
						ImageSurface bm2 = new ImageSurface(Format.Argb32,bm.Width,(int)(ActualHeight*0.95));
						Context g2 = new Context(bm2);
						g2.SetSource(bm,0,ActualHeight-bm.Height);
						g2.Paint();
						((IDisposable)g2).Dispose();
						bm.Destroy();
						bm = bm2;
					}
				}
				else {
					if(bm.Width > ActualWidth) {
						if(ActualHeight > ActualWidth) {
							angle = 90.0;
							bm.Destroy();
							bm = null;
							goto imgretry;
						}
						// crop the end
						ImageSurface bm2 = new ImageSurface(Format.Argb32,(int)(ActualWidth*0.95),bm.Height);
						Context g2 = new Context(bm2);
						g2.SetSource(bm,0,0);
						g2.Paint();
						((IDisposable)g2).Dispose();
						bm.Destroy();
						bm = bm2;
					}
				}
				//WordBitmap bm2 = new WordBitmap(bm);
                Dictionary<Point, List<Point>> lst = CreateCollisionList(bm);
                bool collided = true;
                do
                {
                    Point spiralPoint = GetSpiralPoint(position);
                    int offsetX = (bm.Width / 2);
                    int offsetY = (bm.Height / 2);
                    var testPoint = new Point((int)(spiralPoint.X + centre.X - offsetX), (int)(spiralPoint.Y + centre.Y - offsetY));
                    if (position > (2 * Math.PI) * 580)
                    {
						//bm2.Dispose();
                        if (++currentPoint >= points.Length)
							goto done;
      					goto again;
                    }
                    int cols = CountCollisions(testPoint, lst);
                    if (cols == 0)
                    {
                    tryagain:
                        double oldY = testPoint.Y;
                        if (Math.Abs(testPoint.X + offsetX - centre.X) > 10)
                        {
                            if (testPoint.X + offsetX < centre.X)
                            {
                                do
                                {
                                    testPoint.X += 2;
                                } while (testPoint.X + offsetX < centre.X && CountCollisions(testPoint, lst) == 0);
                                testPoint.X -= 2;
                            }
                            else
                            {
                                do
                                {
                                    testPoint.X -= 2;
                                } while (testPoint.X + offsetX > centre.X && CountCollisions(testPoint, lst) == 0);
                                testPoint.X += 2;
                            }
                        }
                        if (Math.Abs(testPoint.Y + offsetY - centre.Y) > 10)
                        {
                            if (testPoint.Y + offsetY < centre.Y)
                            {
                                do
                                {
                                    testPoint.Y += 2;
                                } while (testPoint.Y + offsetY < centre.Y && CountCollisions(testPoint, lst) == 0);
                                testPoint.Y -= 2;
                            }
                            else
                            {
                                do
                                {
                                    testPoint.Y -= 2;
                                } while (testPoint.Y + offsetY > centre.Y && CountCollisions(testPoint, lst) == 0);
                                testPoint.Y += 2;
                            }
                            if (testPoint.Y != oldY)
                                goto tryagain;
                        }


                        collided = false;
                        CopyBits(testPoint, bm, lst, Entries.IndexOf(e));
						Console.Error.WriteLine("id: {0}, word: {1}, score: {7}, fontsize: {8}," +
							"x: {2}, y: {3}, w: {4}, h: {5}, angle: {6}",id,e.Word,testPoint.X,testPoint.Y,
						                     bm.Width,bm.Height,angle,e.SizeValue,fontsize);
						id++;
						bm.Destroy();
						if(svg != null) {
							svg.SetFontSize(fontsize);
							TextExtents te2 = svg.TextExtents(e.Word);
							svg.Save();

							if(angle == 90.0) {
								svg.MoveTo(testPoint.X-te2.YBearing,testPoint.Y+te2.Width+te2.XBearing);
								svg.Rotate(-0.5*Math.PI);
							}
							else {
								svg.MoveTo(testPoint.X-te2.XBearing,testPoint.Y-te2.YBearing);
							}
							svg.Color = e.Color;
							svg.ShowText(e.Word);
							svg.Restore();
						}
						if(pdf != null) {
							pdf.SetFontSize(fontsize);
							TextExtents te2 = pdf.TextExtents(e.Word);
							pdf.Save();

							if(angle == 90.0) {
								pdf.MoveTo(testPoint.X-te2.YBearing,testPoint.Y+te2.Width+te2.XBearing);
								pdf.Rotate(-0.5*Math.PI);
							}
							else {
								pdf.MoveTo(testPoint.X-te2.XBearing,testPoint.Y-te2.YBearing);
							}
							pdf.Color = e.Color;
							pdf.ShowText(e.Word);
							pdf.Restore();
						}
                    }
                    else
                    {
                        if (cols <= 2)
                        {
                            position += (2 * Math.PI) / 100;
                        }
                        else

                            position += (2 * Math.PI) / 40;
                    }
                } while (collided);
            }
        done:
			((IDisposable)g).Dispose();
			if(svgfn != null) ((IDisposable)svg).Dispose();
			if(pdffn != null) ((IDisposable)pdf).Dispose();
			Console.Error.WriteLine("# {0} words placed",id);
        }

        private int CountCollisions(Point testPoint, Dictionary<Point, List<Point>> lst)
        {
            int testRight = GetCollisions(new Point(testPoint.X + 2, testPoint.Y), lst);
            int testLeft = GetCollisions(new Point(testPoint.X - 2, testPoint.Y), lst);
            int cols = GetCollisions(testPoint, lst) + testRight + testLeft + GetCollisions(new Point(testPoint.X, testPoint.Y + 2), lst) + GetCollisions(new Point(testPoint.X, testPoint.Y - 2), lst);
            return cols;
        }


        //Property MinimumValue


        private void CopyBits(Point testPoint, ImageSurface bm, Dictionary<Point, List<Point>> lst, int index)
        {
            int pixelWidth = _source.Width;
            int mapWidth = pixelWidth / 4;

			g.SetSource(bm,testPoint.X,testPoint.Y);
			g.Paint();

			int sx = (int)testPoint.X / 4;
            int sy = (int)testPoint.Y / 4;
            foreach (Point pt in lst.Select(e => e.Key))
            {
                _mapping[(int)(pt.Y + sy) * mapWidth + (int)(pt.X + sx)] = index;
                _mapping[(int)(pt.Y + sy + 1) * mapWidth + (int)(pt.X + sx)] = index;
                _mapping[(int)(pt.Y + sy + 1) * mapWidth + (int)(pt.X + 1 + sx)] = index;
                _mapping[(int)(pt.Y + sy) * mapWidth + (int)(pt.X + 1 + sx)] = index;
            }
        }

        private Point GetSpiralPoint(double position, double radius = 7)
        {
            double mult = position / (2 * Math.PI) * radius;
            double angle = position % (2 * Math.PI);
            return new Point((int)(mult * Math.Sin(angle)), (int)(mult * Math.Cos(angle)));
        }

        private ImageSurface CreateImage(string text, double fontsize, Color wordColor = default(Color), double angle = 0)
        {
            if (text == string.Empty)
                return new ImageSurface(Format.Argb32, 0, 0);
//			Font font1 = new Font(font,(float)size,FontStyle.Bold);

			g.SetFontSize(fontsize);
			TextExtents te = g.TextExtents(text);


			ImageSurface bm;
			if(angle == 90.0) bm = new ImageSurface(Format.Argb32,(int)Math.Ceiling(te.Height),(int)Math.Ceiling(te.Width));
			else bm = new ImageSurface(Format.Argb32,(int)Math.Ceiling(te.Width),(int)Math.Ceiling(te.Height));
			Context g2 = new Context(bm);
			g2.ScaledFont = g.ScaledFont;
			g2.Color = wordColor;
	/*		g2.Color = new Color(1.0,1.0,1.0);
			g2.Paint();*/
			if(angle == 90.0) {
				g2.MoveTo(-1.0*te.YBearing,te.Width+te.XBearing);
				g2.Rotate(-0.5*Math.PI);
			}
			else g2.MoveTo(-1.0*te.XBearing,-1.0*te.YBearing);
			g2.ShowText(text);
			((IDisposable)g2).Dispose();
   			return bm;
        }


        private Dictionary<Point, List<Point>> CreateCollisionList(ImageSurface bmp)
        {
            var l = new List<Point>();
            int pixelHeight = bmp.Height;
            int pixelWidth = bmp.Width;
            var lookup = new Dictionary<Point, List<Point>>();
			byte[] pixels = bmp.Data;
			int stride = pixelWidth*bytes;

            for (int y = 0; y < pixelHeight; y++)
            {
                for (int x = 0; x < pixelWidth; x++)
                {
                    if ( !( pixels[y*stride + x*bytes + 3] == 0 ) )
//					       pixels[y*stride + x*bytes] == 255 && pixels[y*stride + x*bytes + 1] == 255 && pixels[y*stride + x*bytes + 2] == 255) )
//	note: we only check the alpha value, unused pixels should be left transparent
                    {
                        var detailPoint = new Point(x, y);
                        l.Add(detailPoint);
                        var blockPoint = new Point(((x / 4)), ((y / 4)));
                        if (!lookup.ContainsKey(blockPoint))
                        {
                            lookup[blockPoint] = new List<Point>();
                        }
                        lookup[blockPoint].Add(detailPoint);
                    }
                }
            }
            return lookup;
        }

        private int GetCollisions(Point pt, Dictionary<Point, List<Point>> list)
        {
			byte[] pixels = pixelss;
            int pixelWidth = _source.Width;
            int mapWidth = (_source.Width / 4);
			int stride = strides;

            int c = 0;
            foreach (var pair in list)
            {
                var testPt = new Point(pt.X + pair.Key.X * 4, pt.Y + pair.Key.Y * 4);
                if (testPt.X < 0 || testPt.X >= _source.Width || testPt.Y < 0 || testPt.Y >= _source.Height)
                    return 1;
                int pos = ((((int)pair.Key.Y + (int)(pt.Y / 4)) * mapWidth) + (int)pair.Key.X + ((int)(pt.X / 4)));
                try
                {
                    if (_mapping[pos] != -1 || _mapping[pos + 1] != -1 || _mapping[pos + mapWidth] != -1 || _mapping[pos + mapWidth + 1] != -1)
                    {
                        foreach (Point p in pair.Value)
                        {
                            var nx = (int)(p.X + pt.X);
                            var ny = (int)(pt.Y + p.Y);
                            if (nx < 0 || nx >= _source.Width || ny < 0 || ny >= _source.Height)
                                return 1;
                            if( !( pixels[ny*stride + nx*bytes] == 255 && pixels[ny*stride + nx*bytes + 1] == 255 && pixels[ny*stride + nx*bytes + 2] == 255) ) return 1;
                        }
                    }
                }
                catch (Exception)
                {
                    return 1;
                }
            }
            return 0;
        }

		public void Save(string fn) {
			_source.WriteToPng(fn);
		}
       

        public class WordCloudEntry
        {
            public string Word { get; set; }
            public double SizeValue { get; set; }
            public double ColorValue { get; set; }
            public object Tag { get; set; }
            public double Angle { get; set; }
            public Color Color { get; set; }
        }

		/*
		public class WordBitmap {
			public byte[] pixels;
			public int width;
			public int height;
			public int stride;

			private BitmapData bitmapData;
			private Bitmap bmps;

			public WordBitmap(Bitmap bmp) {
				//copy the pixels
				bitmapData = bmp.LockBits(
	                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
	                ImageLockMode.ReadOnly,
	                PixelFormat.Format32bppArgb);
				int numbytes = Math.Abs(bitmapData.Stride) * bitmapData.Height;
	            pixels = new byte[numbytes];

	            // Copy the RGB values into the array.
	            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixels, 0, numbytes);
	            stride = bitmapData.Stride;
				bmps = bmp;
				width = bmp.Width;
				height = bmp.Height;
			}

			public void Dispose() {
				bmps.UnlockBits(bitmapData);
				bitmapData = null;
				pixels = null;
			}
		} */


		public static void Main(string[] args) {
			string fnin = null;
			string fnout = null;
			string fontt = null;
			string svgfn = null;
			string pdffn = null;
			string cf = null;
			bool cd = false;

			for(int i=0;i<args.Length;i++) if(args[i][0] == '-') switch(args[i][1]) {
			case 'i':
				fnin = args[i+1];
				break;
			case 'o':
				fnout = args[i+1];
				break;
			case 'f':
				fontt = args[i+1];
				break;
			case 'c':
				cf = args[i+1];
				break;
			case 'd':
				cd = true;
				break;
			case 's':
				svgfn = args[i+1];
				break;
			case 'p':
				pdffn = args[i+1];
				break;
			default:
				Console.Error.WriteLine("Unknown switch: {0}!",args[i]);
				break;
			}

			if(cd) {
				if(cf == null) {
					Console.Error.WriteLine("Error: No configfile specified!");
					return;
				}
				StreamWriter sw = new StreamWriter(cf);
				WordCloud cloud1 = new WordCloud();
				XmlSerializer s = new XmlSerializer(typeof(WordCloud));
				s.Serialize(sw,cloud1);
				sw.Close();
				return;
			}

			if(fnin == null || (fnout == null && pdffn == null && svgfn == null)) {
				Console.Error.WriteLine("Error: no input or output file name given!");
				return;
			}

			WordCloud cloud;
			if(cf != null) {
				StreamReader s1 = new StreamReader(cf);
				XmlSerializer s = new XmlSerializer(typeof(WordCloud));
				cloud = (WordCloud)s.Deserialize(s1);
			}
			else cloud = new WordCloud();


			if(fontt != null) {
				cloud.fontname = fontt;
			}

			cloud.svgfn = svgfn;
			cloud.pdffn = pdffn;

			StreamReader sr = new StreamReader(fnin);	

			Color c = new Color(0,0,0);
			int r,g,b;
			if(cloud.colorstring != null) 
				if(cloud.colorstring.Length >= 7)
					if(cloud.colorstring[0] == '#')
	if(int.TryParse(cloud.colorstring.Substring(1,2),System.Globalization.NumberStyles.AllowHexSpecifier,null,out r)
	 && int.TryParse(cloud.colorstring.Substring(3,2),System.Globalization.NumberStyles.AllowHexSpecifier,null,out g)
	 && int.TryParse(cloud.colorstring.Substring(5,2),System.Globalization.NumberStyles.AllowHexSpecifier,null,out b))
							c = new Color( ((double)r) / 255.0, ((double)g) / 255.0, ((double)b) / 255.0 );

			cloud.Entries = new List<WordCloudEntry>();
			while(!sr.EndOfStream) {
				string line = sr.ReadLine();
				if(line == null) break;
				if(line.Length == 0) break;
				string[] e = line.Split(new char[]{':'});
				if(e.Length < 2) break;
				WordCloudEntry e2 = new WordCloudEntry();
				e2.Word = e[0];
				e2.SizeValue = Convert.ToDouble(e[1]);
				e2.ColorValue = 0.0;
				e2.Color = c;
				e2.Angle = 0.0;
				cloud.Entries.Add(e2);
			}
			sr.Close();
			cloud.InternalRegenerateCloud();
			if(fnout != null) cloud.Save(fnout);
		}
    }
}


