﻿using System.Drawing;
using System.Collections.Generic;
using ceometric.DelaunayTriangulator;
using Android.Graphics;
using Java.Lang;
using Java.Util;
using Double = System.Double;
using Enum = System.Enum;
using Math = System.Math;
using PointF = System.Drawing.PointF;

namespace LowPolyLibrary
{
	public class LowPolyLib
	{
		private DelaunayTriangulation2d _delaunay = new DelaunayTriangulation2d();
		private List<ceometric.DelaunayTriangulator.Point> _points;
	    private List<Triangle> triangulatedPoints;
        public int boundsWidth;
		public int boundsHeight;
		public double cell_size = 75;
		public double setVariance = .75;
		private double calcVariance, cells_x, cells_y;
		private double bleed_x, bleed_y;
		private static int numFrames = 12; //static necessary for creation of framedPoints list
		List<System.Drawing.PointF>[] framedPoints = new List<System.Drawing.PointF>[numFrames];

		Bitmap gradient;

        Dictionary<System.Drawing.PointF, List<Triangle>> poTriDic = new Dictionary<System.Drawing.PointF, List<Triangle>>();

		System.Random rand = new System.Random();

		public Bitmap GenerateNew()
		{
			UpdateVars();
			_points = GeneratePoints();
			return createBitmap();
		}

		private void UpdateVars()
		{
			calcVariance = cell_size * setVariance / 2;
			cells_x = Math.Floor((boundsWidth + 4 * cell_size) / cell_size);
			cells_y = Math.Floor((boundsHeight + 4 * cell_size) / cell_size);
			bleed_x = ((cells_x * cell_size) - boundsWidth) / 2;
			bleed_y = ((cells_y * cell_size) - boundsHeight) / 2;
		}

		private Bitmap createBitmap()
		{
			Bitmap drawingCanvas = Bitmap.CreateBitmap (boundsWidth, boundsHeight, Bitmap.Config.Rgb565);
			Canvas canvas = new Canvas (drawingCanvas);

			Paint paint = new Paint();

			paint.StrokeWidth = .5f;
			paint.SetStyle (Paint.Style.FillAndStroke);
			paint.AntiAlias = true;
            
            var overlays = createOverlays();
            for (int i = 0; i < framedPoints.Length; i++)
		    {
		        framedPoints[i] = new List<System.Drawing.PointF>();
		    }

			gradient = getGradient();
			triangulatedPoints = _delaunay.Triangulate(_points);
			//generating a new base triangulation. if an old one exists get rid of it
			if (poTriDic != null)
				poTriDic = new Dictionary<System.Drawing.PointF, List<Triangle>>();

			for (int i = 0; i < triangulatedPoints.Count; i++)
			{
				System.Drawing.PointF a = new System.Drawing.PointF ((float)triangulatedPoints [i].Vertex1.X, (float)triangulatedPoints [i].Vertex1.Y);
				System.Drawing.PointF b = new System.Drawing.PointF ((float)triangulatedPoints [i].Vertex2.X, (float)triangulatedPoints [i].Vertex2.Y);
				System.Drawing.PointF c = new System.Drawing.PointF ((float)triangulatedPoints [i].Vertex3.X, (float)triangulatedPoints [i].Vertex3.Y);
                
			    Path trianglePath = drawTrianglePath(a, b, c);

				var center = centroid(triangulatedPoints[i]);

                //animation logic
                divyTris(a, overlays, i);
                divyTris(b, overlays, i);
                divyTris(c,overlays, i);

                paint.Color = getTriangleColor (gradient, center);

				canvas.DrawPath (trianglePath, paint);
			}
			return drawingCanvas;
		}

		private Bitmap drawFrame(Dictionary<System.Drawing.PointF, List<Triangle>> frameDic)
		{
			Bitmap drawingCanvas = Bitmap.CreateBitmap(boundsWidth, boundsHeight, Bitmap.Config.Rgb565);
			Canvas canvas = new Canvas(drawingCanvas);

			Paint paint = new Paint();
			paint.StrokeWidth = .5f;
			paint.SetStyle(Paint.Style.FillAndStroke);
			paint.AntiAlias = true;

			foreach (KeyValuePair<System.Drawing.PointF, List<Triangle>> entry in frameDic)
			{
				// do something with entry.Value or entry.Key
				var frameTriList = entry.Value;
				foreach (var tri in frameTriList)
				{
					System.Drawing.PointF a = new System.Drawing.PointF((float)tri.Vertex1.X, (float)tri.Vertex1.Y);
					System.Drawing.PointF b = new System.Drawing.PointF((float)tri.Vertex2.X, (float)tri.Vertex2.Y);
					System.Drawing.PointF c = new System.Drawing.PointF((float)tri.Vertex3.X, (float)tri.Vertex3.Y);

					Path trianglePath = drawTrianglePath(a, b, c);

					var center = centroid(tri);

					paint.Color = getTriangleColor(gradient, center);

					canvas.DrawPath(trianglePath, paint);
				}
			}
			return drawingCanvas;
		}

		public Bitmap createAnimBitmap(int frame)
		{
			var frameDic = makeFrame(frame, 24);
			var frameBitmap = drawFrame(frameDic);
			return frameBitmap;
		}

		private Dictionary<System.Drawing.PointF, List<Triangle>>[] makeAnimation(int numFrames2)
		{
			var animationFrames = new Dictionary<System.Drawing.PointF, List<Triangle>>[numFrames2];
			for (int i = 0; i < numFrames2; i++)
			{
				animationFrames[i] = makeFrame(i, numFrames2);
			}
			return animationFrames;
		}

		private Dictionary<System.Drawing.PointF, List<Triangle>> makeFrame(int frameNum, int totalFrames)
	    {
			//temporary copy of the poTriDic. This copy will serve as a 'frame' in the animationFrames array
			var tempPoTriDic = new Dictionary<System.Drawing.PointF, List<Triangle>>(poTriDic);
			//get array of points contained in a specified frame
	        var pointList = framedPoints[frameNum];
			var direction = get360Direction();
	        
			foreach (var workingPoint in pointList)
	        {
                //get list of tris at given workingPoint in given frame
	            var tris = tempPoTriDic[workingPoint];

                var distCanMove = shortestDistance(workingPoint, tris, direction);
				var xComponent = getXComponent(direction, distCanMove)/12;
				var yComponent = getYComponent(direction, distCanMove)/12;
				foreach (var triangle in tris)
				{
					//animate each triangle
					//triangle.animate();
					if (triangle.Vertex1.X.CompareTo(workingPoint.X) == 0 && triangle.Vertex1.Y.CompareTo(workingPoint.Y) == 0){
						triangle.Vertex1.X += xComponent;//frameLocation(frameNum, totalFrames, xComponent);
						triangle.Vertex1.Y += yComponent;//frameLocation(frameNum, totalFrames, yComponent);
					}
					else if (triangle.Vertex2.X.CompareTo(workingPoint.X) == 0 && triangle.Vertex2.Y.CompareTo(workingPoint.Y) == 0){
						triangle.Vertex2.X += xComponent;//frameLocation(frameNum, totalFrames, xComponent);
						triangle.Vertex2.Y += yComponent;//frameLocation(frameNum, totalFrames, yComponent);
					}
					else if (triangle.Vertex3.X.CompareTo(workingPoint.X) == 0 && triangle.Vertex3.Y.CompareTo(workingPoint.Y) == 0){
						triangle.Vertex3.X += xComponent;//frameLocation(frameNum, totalFrames, xComponent);
						triangle.Vertex3.Y += yComponent;//frameLocation(frameNum, totalFrames, yComponent);
					}
				}
	        }
	        return tempPoTriDic;
	    }

		private double frameLocation(int frame, int totalFrames, Double distanceToCcover)
		{
			var ratioToFinalMovement = frame / (Double)totalFrames;
			var thisCoord = ratioToFinalMovement * distanceToCcover;
			return thisCoord;
		}

		private double getXComponent(int angle, double length)
		{
			return length * Math.Cos(angle);
		}

		private double getYComponent(int angle, double length)
		{
			return length * Math.Sin(angle);
		}

		private int get360Direction()
		{
			//return a int from 0 to 359 that represents the direction a point will move
			return rand.Next(360);
		}

		private List<Triangle> quadList(List<Triangle> tris, int degree, PointF workingPoint)
		{
			var direction = "empty";

			if (degree > 270)
				direction = "quad4";
			else if (degree > 180)
				direction = "quad3";
			else if (degree > 90)
				direction = "quad2";
			else
				direction = "quad1";

			var quad1 = new List<Triangle>();
			var quad2 = new List<Triangle>();
			var quad3 = new List<Triangle>();
			var quad4 = new List<Triangle>();

			foreach (var tri in tris)
			{
				//var angle = getAngle(workingPoint, centroid(tri));
				var triCenter = centroid(tri);
				//if x,y of new triCenter > x,y of working point, then in the 1st quardant
				if (triCenter.X > workingPoint.X && triCenter.Y > workingPoint.Y)
					quad1.Add(tri);
				else if (triCenter.X < workingPoint.X && triCenter.Y > workingPoint.Y)
					quad2.Add(tri);
				else if (triCenter.X > workingPoint.X && triCenter.Y < workingPoint.Y)
					quad3.Add(tri);
				else if(triCenter.X > workingPoint.X && triCenter.Y < workingPoint.Y)
					quad4.Add(tri);
			}
			switch (direction)
			{
				case "quad1":
					return quad1;
				case "quad2":
					return quad2;
				case "quad3":
					return quad3;
				case "quad4":
					return quad4;
				default:
					return quad1;
			}

		}

        private double shortestDistance(PointF workingPoint, List<Triangle> tris, int degree)
        {
			var quadTris = quadList(tris, degree, workingPoint);

			//shortest distance between a workingPoint and all points of a tri
			double shortest = -1;
            foreach (var tri in quadTris)
            {
                //get distances between a workingPoint and each vertex of a tri
                var vert1Distance = dist(workingPoint, tri.Vertex1);
                var vert2Distance = dist(workingPoint, tri.Vertex2);
                var vert3Distance = dist(workingPoint, tri.Vertex3);

                double tempShortest;
                //only one vertex distance can be 0. So if vert1 is 0, assign vert 2 for initial distance comparrison
                //(will be changed later if there is a shorter distance)
                if (vert1Distance.CompareTo(0) == 0) // if ver1Distance == 0
                    tempShortest = vert2Distance;
                else
                    tempShortest = vert1Distance;
                //if a vertex distance is less than the current tempShortest and not 0, it is the new shortest distance
                if (vert1Distance < tempShortest && vert1Distance.CompareTo(0) == 0)// or if vertice == 0
                    tempShortest = vert1Distance;
                if (vert2Distance < tempShortest && vert2Distance.CompareTo(0) == 0)// or if vertice == 0
                    tempShortest = vert2Distance;
                if (vert3Distance < tempShortest && vert3Distance.CompareTo(0) == 0)// or if vertice == 0
                    tempShortest = vert3Distance;
                //tempshortest is now the shortest distance between a workingPoint and tri vertices, save it
                //if this is the first run (shortest == -1) then tempShortest is the smalled distance
                if (shortest.CompareTo(-1) == 0) //if shortest == -1
                    shortest = tempShortest;
                //if not the first run, only assign shortest if tempShortest is smaller
                else
                    if (tempShortest<shortest)
                        shortest = tempShortest;

            }
            return shortest;
        }

	    private double dist(PointF workingPoint, ceometric.DelaunayTriangulator.Point vertex)
	    {
            var xSquare = (workingPoint.X + vertex.X) * (workingPoint.X + vertex.X);
            var ySquare = (workingPoint.Y + vertex.Y) * (workingPoint.Y + vertex.Y);
            return Math.Sqrt(xSquare + ySquare);
        }

        private void divyTris(System.Drawing.PointF point, RectangleF[] overlays, int arrayLoc)
	    {
            //if the point/triList distionary has a point already, add that triangle to the list at that key(point)
            if (poTriDic.ContainsKey(point))
                poTriDic[point].Add(triangulatedPoints[arrayLoc]);
            //if the point/triList distionary doesnt not have a point, initialize it, and add that triangle to the list at that key(point)
            else
            {
                poTriDic[point] = new List<Triangle>();
                poTriDic[point].Add(triangulatedPoints[arrayLoc]);
            }
            for (int j = 0; j < overlays.Length; j++)
            {
                //if the rectangle overlay contains a point
                if (overlays[j].Contains(point))
                {
                    //if the point has not already been added to the overlay's point list
                    if(!framedPoints[j].Contains(point))
                        //add it
                        framedPoints[j].Add(point);
                }
            }
	    }

	    private RectangleF[] createOverlays()
	    {
            //first and last rectangles need to be wider to cover points that are outside to the left and right of the pic bounds
			//all rectangles need to be higher and lower than the pic bounds to cover points above and below the pic bounds

			//get width of frame when there are 12 rectangles on screen
            var frameWidth = boundsWidth / numFrames;
            //represents the left edge of the rectangles
            var currentX = 0;
			//array size numFrames of rectangles. each array entry serves as a rectangle(i) starting from the left
            RectangleF[] frames = new RectangleF[numFrames];

            #region AllPointsLogic
            //this logic is for grabbing all points (even those outside the visible drawing area)
            //        var tempWidth = boundsWidth / 2;
            //        var tempHeight = boundsHeight / 2;
            //        for (int i = 0; i < numFrames; i++)
            //        {
            //System.Drawing.RectangleF overlay;
            ////if the first rectangle
            //if (i == 0)
            //	overlay = new RectangleF(currentX - tempWidth, 0 - tempHeight, frameWidth + tempWidth, boundsHeight + (tempHeight*2));
            ////if the last rectangle
            //else if (i == numFrames - 1)
            //	overlay = new RectangleF(currentX, 0 - tempHeight, frameWidth + tempWidth, boundsHeight + (tempHeight * 2));
            //else
            //	overlay = new RectangleF(currentX, 0 - tempHeight, frameWidth, boundsHeight + (tempHeight * 2));

            //            frames[i] = overlay;
            //            currentX += frameWidth;
            //        }
            #endregion
            //logic for grabbing points only in visible drawing area
            for (int i = 0; i < numFrames; i++)
            {
                RectangleF overlay = new RectangleF(currentX, 0, frameWidth, boundsHeight);

                frames[i] = overlay;
                currentX += frameWidth;
            }

            return frames;
	    }

	    private Path drawTrianglePath(System.Drawing.PointF a, System.Drawing.PointF b, System.Drawing.PointF c)
	    {
            Path path = new Path();
            path.SetFillType(Path.FillType.EvenOdd);
            path.MoveTo(b.X, b.Y);
            path.LineTo(c.X, c.Y);
            path.LineTo(a.X, a.Y);
            path.Close();
            return path;
        }

		private Android.Graphics.Color getTriangleColor(Bitmap gradient, System.Drawing.Point center)
		{
		    center = keepInPicBounds(center);

			System.Drawing.Color colorFromRGB;
			try
			{
				colorFromRGB = System.Drawing.Color.FromArgb(gradient.GetPixel(center.X, center.Y));
			}
			catch
			{
				colorFromRGB = System.Drawing.Color.Cyan;
			}

			Android.Graphics.Color triColor = Android.Graphics.Color.Rgb (colorFromRGB.R, colorFromRGB.G, colorFromRGB.B);
			return triColor;
		}

	    private System.Drawing.Point keepInPicBounds(System.Drawing.Point center)
	    {
            if (center.X < 0)
                center.X += (int)bleed_x;
            else if (center.X > boundsWidth)
                center.X -= (int)bleed_x;
            else if (center.X == boundsWidth)
                center.X -= (int)bleed_x - 1;
            if (center.Y < 0)
                center.Y += (int)bleed_y;
            else if (center.Y > boundsHeight)
                center.Y -= (int)bleed_y + 1;
            else if (center.Y == boundsHeight)
                center.Y -= (int)bleed_y - 1;
	        return center;
	    }

		private	int[] getGradientColors()
		{
			//get all gradient codes
			var values = Enum.GetValues(typeof(ColorBru.Code));
			ColorBru.Code randomCode = (ColorBru.Code)values.GetValue(rand.Next(values.Length));
			//gets specified colors in gradient length: #
			var brewColors = ColorBru.GetHtmlCodes (randomCode, 6);
			//array of ints converted from brewColors
			var colorArray = new int[brewColors.Length];
			for (int i = 0; i < brewColors.Length; i++) {
				colorArray [i] = Android.Graphics.Color.ParseColor (brewColors [i]);
			}
			return colorArray;
		}

		private Bitmap getGradient()
		{
			var colorArray = getGradientColors ();

			Shader gradientShader;

			switch (rand.Next(3)) {
			case 0:
				gradientShader = new LinearGradient (
					                      0,
					                      0,
					                      boundsWidth,
					                      boundsHeight,
					                      colorArray,
					                      null,
					                      Shader.TileMode.Repeat
				                      );
				break;
			case 1:
				gradientShader = new SweepGradient (
					((float)boundsWidth / 2),
					((float)boundsHeight / 2),
					colorArray,
					//new float[]{ }
					null
				);
				break;
			case 2:
				gradientShader = new RadialGradient (
					                        ((float)boundsWidth / 2),
					                        ((float)boundsHeight / 2),
					                        ((float)boundsWidth / 2),
					                        colorArray,
					                        null,
					                        Shader.TileMode.Clamp
				                        );
				break;
			default:
				gradientShader = new LinearGradient (
					0,
					0,
					boundsWidth,
					boundsHeight,
					colorArray,
					null,
					Shader.TileMode.Repeat
				);
				break;
			}

//			LinearGradientBrush brush = new LinearGradientBrush(
//				new System.Drawing.Point(0,0),
//				new System.Drawing.Point(width, height), 
//				Color.FromArgb(255,0,0,255),
//				Color.FromArgb(255,0,255,0));

//			Bitmap temp = new Bitmap(width, height);
			Bitmap bmp = Bitmap.CreateBitmap (boundsWidth, boundsHeight, Bitmap.Config.Rgb565);
//			Graphics graphics = Graphics.FromImage(temp);
			Canvas canvas = new Canvas (bmp);
			Paint pnt = new Paint();
			pnt.SetStyle (Paint.Style.Fill);
			pnt.SetShader (gradientShader);
			canvas.DrawRect(0,0,boundsWidth,boundsHeight,pnt);
			return bmp;
		}

		public List<ceometric.DelaunayTriangulator.Point> GeneratePoints()
		{
			var points = new List<ceometric.DelaunayTriangulator.Point>();
			for (var i = - bleed_x; i < boundsWidth + bleed_x; i += cell_size) 
			{
				for (var j = - bleed_y; j < boundsHeight + bleed_y; j += cell_size) 
				{
					var x = i + cell_size/2 + _map(rand.NextDouble(),new int[] {0, 1},new double[] {-calcVariance, calcVariance});
					var y = j + cell_size/2 + _map(rand.NextDouble(),new int[] {0, 1},new double[] {-calcVariance, calcVariance});
					points.Add(new ceometric.DelaunayTriangulator.Point(Math.Floor(x),Math.Floor(y),0));
				}
			}
			return points;
		}

		private double _map(double num, int[] in_range, double[] out_range)
		{
			return (num - in_range[0]) * (out_range[1] - out_range[0]) / (in_range[1] - in_range[0]) + out_range[0];
		}

		private System.Drawing.Point centroid(Triangle triangle)
		{
			int x = (int)((triangle.Vertex1.X + triangle.Vertex2.X + triangle.Vertex3.X)/3);
			int y = (int)((triangle.Vertex1.Y + triangle.Vertex2.Y + triangle.Vertex3.Y) / 3);

			return new System.Drawing.Point(x,y);
		}
	}
}

