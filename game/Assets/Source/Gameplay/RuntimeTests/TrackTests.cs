using UnityEngine;
using NUnit.Framework;

namespace Race.Gameplay.Tests
{
    // TODO: more tests; these helped me catch a bug, but there are more.
    public class TrackTests
    {
        internal StraightTrack track;

        [SetUp]
        public void Setup()
        {
            var start = new Vector3(1, 1, 1);
            var end = new Vector3(1, 1, 10);
            var width = 3;
            track = new StraightTrack(start, end, width);
        }

        [Test]
        public void StartingSegment_IsValidAndInsideTrack()
        {
            var startPoint = GetStartPoint();
            Assert.True(startPoint.IsValid);
            Assert.True(startPoint.IsInsideTrack);
        }

        private RoadPoint GetStartPoint()
        {
            var startSegment = track.StartingSegment;
            var startPoint = RoadPoint.CreateStartOf(startSegment);
            return startPoint;
        }

        private static void AssertClose(Vector3 expected, Vector3 actual, float epsilon, string message = "")
        {
            Assert.AreEqual(expected.x, actual.x, epsilon, message);
            Assert.AreEqual(expected.y, actual.y, epsilon, message);
            Assert.AreEqual(expected.z, actual.z, epsilon, message);
        }

        [Test]
        public void QueriedInitialPosition_IsTheSameAsTheActualInitialPosition()
        {
            var startPoint = GetStartPoint();
            var positionOfRoadStart = track.GetRoadMiddlePosition(startPoint);
            AssertClose(track._startPosition, positionOfRoadStart, 0.01f);
        }

        private Vector3 GetCenterOfTrack()
        {
            return (track._endPosition + track._startPosition) / 2;
        }

        [Test]
        public void PointInsideTrack_StaysInsideTrackAfterUpdate()
        {
            float t = 0.3f;
            var startSegment = track.StartingSegment;
            var startPoint = new RoadPoint(startSegment, t);

            Assert.That(startPoint.IsInsideTrack);
            var position = track.GetRoadMiddlePosition(startPoint);
            var updated = track.UpdateRoadPoint(startPoint, position);
        }

        [Test]
        public void FinalPoint_HasProportionOf1()
        {
            var point = track.GetRoadPointAt(track._endPosition);
            Assert.AreEqual(1.0f, point.position, 0.01f);
        }

        [Test]
        public void QueringCentralPoint_GetsCenter()
        {
            var centerPoint = new RoadPoint(0, 0.5f);
            var centerPos = track.GetRoadMiddlePosition(centerPoint);
            AssertClose(GetCenterOfTrack(), centerPos, 0.01f);
        }

        [Test]
        public void CenterPoint_HasHalfPositionProportion()
        {
            var center = GetCenterOfTrack();
            var point = track.GetRoadPointAt(center);
            Assert.AreEqual(0.5f, point.position, 0.01f);
        }


        [Test]
        public void Position_ToPoint_ToPosition_AreEqual()
        {
            var positionOnRoad = GetCenterOfTrack();
            var point = track.GetRoadPointAt(positionOnRoad);
            var positionAfter = track.GetRoadMiddlePosition(point);
            AssertClose(positionOnRoad, positionAfter, 0.01f);
        }
    }
}