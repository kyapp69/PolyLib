﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using DelaunayTriangulator;
using SkiaSharp;

namespace LowPolyLibrary.Animation
{
    class Touch : AnimationBase
    {
        private int _lowerBound;
        private int _upperBound;

        public List<SKPoint> InRange;
        public List<SKPoint> InRangOfRecs;
        public List<SKPoint> OutOfRange;
        public SKPoint TouchLocation;
        public int TouchRadius;

        internal Touch(Triangulation triangulation, float x, float y, int radius) : base(triangulation)
        {
			AnimationType = AnimationTypes.Type.Touch;

            InRange = new List<SKPoint>();
            InRangOfRecs = new List<SKPoint>();
            OutOfRange = new List<SKPoint>();
            TouchLocation = new SKPoint(x, y);
            TouchRadius = radius;
        }

        internal List<SKPoint> getTouchAreaRecPoints(int currentIndex, int displacement = 0)
        {
            var touch = new List<SKPoint>();

            currentIndex += displacement;

            var firstFrame = 0;
            var lastFrame = viewRectangles.VisibleRecs.Length - 1;

            if (currentIndex < firstFrame)
            {
                currentIndex++;
            }
            if (currentIndex > lastFrame)
            {
                currentIndex--;
            }

            if (displacement == 0)
            {
                _lowerBound = currentIndex;
                _upperBound = currentIndex;
                if (currentIndex != firstFrame &&
                    viewRectangles.VisibleRecs[currentIndex].circleContainsPoints(TouchLocation,
                                                                         TouchRadius,
                                                                         viewRectangles.VisibleRecs[currentIndex].A,
                                                                         viewRectangles.VisibleRecs[currentIndex].D))
                {
                    touch.AddRange(getTouchAreaRecPoints(currentIndex, -1));
                }
                if (currentIndex != lastFrame &&
                    viewRectangles.VisibleRecs[currentIndex].circleContainsPoints(TouchLocation,
                                                                         TouchRadius,
                                                                         viewRectangles.VisibleRecs[currentIndex].B,
                                                                         viewRectangles.VisibleRecs[currentIndex].C))
                {
                    touch.AddRange(getTouchAreaRecPoints(currentIndex, 1));
                }
            }
            else
            {

                if (displacement < 0)
                {
                    _lowerBound = currentIndex;
                    if (currentIndex != firstFrame &&
                        viewRectangles.VisibleRecs[currentIndex].circleContainsPoints(TouchLocation,
                                                                             TouchRadius,
                                                                             viewRectangles.VisibleRecs[currentIndex].A,
                                                                             viewRectangles.VisibleRecs[currentIndex].D))
                    {
                        touch.AddRange(getTouchAreaRecPoints(currentIndex, -1));
                    }
                }
                else if (displacement > 0)
                {
                    _upperBound = currentIndex;
                    if (currentIndex != lastFrame &&
                             viewRectangles.VisibleRecs[currentIndex].circleContainsPoints(TouchLocation,
                                                                                  TouchRadius,
                                                                                  viewRectangles.VisibleRecs[currentIndex].B,
                                                                                  viewRectangles.VisibleRecs[currentIndex].C))
                    {
                        touch.AddRange(getTouchAreaRecPoints(currentIndex, 1));
                    }
                }
            }



            touch.AddRange(FramedPoints[currentIndex]);
            return touch;
        }

        internal void setPointsAroundTouch()
        {
            //index of the smaller rectangle that contains the touch point
            //var index = Array.FindIndex(viewRectangles[0], rec => rec.isInsideCircle(touch, radius));
            var index = Array.FindIndex(viewRectangles.VisibleRecs, rec => rec.Contains(TouchLocation));
            //get all points in the same rec as the touch area
            InRange = getTouchAreaRecPoints(index);

            //add the points from recs outside of the touch area to a place we can handle them later
            for (int i = 0; i < FramedPoints.Length; i++)
            {
                if (!(_lowerBound < i && i < _upperBound))
                    OutOfRange.AddRange(FramedPoints[i]);
                OutOfRange.AddRange(WideFramedPoints[i]);
            }

            //actually ween down the points in the touch area to the points inside the "circle" touch area
            var removeFromTouchPoints = new List<SKPoint>();
            foreach (var point in InRange)
            {

                if (!Geometry.pointInsideCircle(point, TouchLocation, TouchRadius))
                {
                    //touchPointLists.inRange.Remove(point);
                    removeFromTouchPoints.Add(point);
                    InRangOfRecs.Add(point);
                }
            }
            foreach (var point in removeFromTouchPoints)
            {
                InRange.Remove(point);
            }
        }

		//helper for "push away from touch"
		internal double frameLocation(int frame, int totalFrames, Double distanceToCcover)
		{
			//method to be used when a final destination is known, and you want to get a proportional distance to have moved up to that point at this point in time
			var ratioToFinalMovement = frame / (Double)totalFrames;
			var thisCoord = ratioToFinalMovement * distanceToCcover;
			return thisCoord;
		}

        internal override void SetupAnimation()
        {
            base.SetupAnimation();

            setPointsAroundTouch();

            IsSetup = true;
        }

        internal override List<AnimatedPoint> RenderFrame()
        {
			var animatedPoints = new List<AnimatedPoint>();
            //var pointsForMeasure = new List<SKPoint>();
            //pointsForMeasure.AddRange(InRange);
            //pointsForMeasure.AddRange(InRangOfRecs);
            var rand = new Random();
            foreach (var point in InRange)
            {
                //var direction = (int)getPolarCoordinates(touch, wPoint);

                //var distCanMove = shortestDistanceFromPoints(point, pointsForMeasure, direction);
                //var distCanMove = 20;
                //var xComponent = getXComponent(direction, distCanMove);
                //var yComponent = getYComponent(direction, distCanMove);

                var xComponent = rand.Next(-10, 10);
                var yComponent = rand.Next(-10, 10);

				var animPoint = new AnimatedPoint(point, xComponent, yComponent);
				animatedPoints.Add(animPoint);
            }
			return animatedPoints;
        }

		//only overriding to force display of red ring of current touch area
        internal override void DrawPointFrame(SKSurface surface, List<AnimatedPoint> pointChanges)
        {
            //base DrawSKPointrame will render the animation correctly, get the bitmap
            base.DrawPointFrame(surface, pointChanges);

            //Create a canvas to draw touch location on the bitmap
            using (var canvas = surface.Canvas)
            {
                //canvas not cleared here bc it is done in the base method above
                using (var paint = new SKPaint())
                {
                    paint.Style = SKPaintStyle.Stroke;
                    paint.Color = new SKColor(247, 77, 77);

                    canvas.DrawCircle(TouchLocation.X, TouchLocation.Y, TouchRadius, paint);
                }
            }
        }
    }
}
