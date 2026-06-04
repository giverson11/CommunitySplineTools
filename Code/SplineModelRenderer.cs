using System;
using NativeEngine;
using System.Runtime.InteropServices;

namespace Sandbox;

public sealed class SplineModelRenderer : ModelRenderer
{
	[Property, Category( "Spline" )]
	public SplineComponent Spline
	{
		get;
		set
		{
			field?.Spline.SplineChanged -= UpdateObject;
			field = value;
			if ( Enabled )
			{
				value.Spline.SplineChanged += UpdateObject;
				UpdateObject();
			}
		}
	}

	[Property, Category( "Spline" )]
	public Rotation ModelRotation
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Rotation.Identity;

	[Property, Category( "Spline" )]
	public Vector3 ModelScale
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Vector3.One;


	[Property, Category( "Spline" )]
	public Vector3 ModelOffset
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	}


	[Property, Category( "Spline" )]
	public bool UseRotationMinimizingFrames
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = true;


	private Mesh customMesh;
	private Model customModel = Model.Error;

	private Vertex[] vertexScratch;
	private int[] indexScratch;

	// Describes one contiguous slice of the merged buffer (either the repeating main mesh or a cap).
	// Filled in two passes: counts + material first to plan the layout, then verts/indices written
	// at VertexStart / IndexStart.
	private struct Segment
	{
		public Vertex[] SrcVerts;
		public uint[] SrcIndices;
		public Material Material;
		public Rotation DeformRotation;
		public Vector3 DeformOffset;
		public Vector3 DeformScale;
		public float SrcMin;
		public float SrcSize;
		public float SegmentStart;
		public float DistancePerCopyWithSpacing;
		public int Copies;
		public float Spacing;

		public int VertexStart;
		public int IndexStart;
		public int VertexCount => SrcVerts.Length * Copies;
		public int IndexCount => SrcIndices.Length * Copies;
	}

	[Property, Category( "Spline" ), MinMax( 0, float.PositiveInfinity )]
	public float Spacing
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = 0f;


	[Property, Category( "Spline" )]
	public bool FlexFit
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = false;


	[Property, FeatureEnabled( "Start Cap" )]
	public bool StartCapEnabled
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	}

	[Property, FeatureEnabled( "End Cap" )]
	public bool EndCapEnabled
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	}

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Model StartCap
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	}


	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Rotation StartCapRotation
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Rotation.Identity;


	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Vector3 StartCapScale
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Vector3.One;


	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Vector3 StartCapOffset
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Vector3.Zero;


	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Model EndCap
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	}

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Rotation EndCapRotation
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Rotation.Identity;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Vector3 EndCapScale
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Vector3.One;


	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Vector3 EndCapOffset
	{
		get;
		set
		{
			field = value;
			UpdateObject();
		}
	} = Vector3.Zero;


	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Spline.IsValid() )
		{
			Spline.Spline.SplineChanged += UpdateObject;
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		customMesh = null;

		if ( Spline.IsValid() )
		{
			Spline.Spline.SplineChanged -= UpdateObject;
		}
	}

	protected override void DrawGizmos()
	{
		//var rotatedModelBounds = Model.Bounds.Rotate( ModelRotation );
		//var sizeInModelDir = rotatedModelBounds.Size.Dot( Vector3.Forward );

		//var minInModelDir = rotatedModelBounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		//var splineLength = Spline.GetLength();
		//var meshesRequired = (int)Math.Ceiling( splineLength / sizeInModelDir );
		//var distancePerMesh = splineLength / meshesRequired;

		//var frames = CalculateTangentFramesUsingUpDir( meshesRequired, 16, distancePerMesh );

		//float arrowLength = distancePerMesh / 4;

		//foreach ( var frame in frames )
		//{
		//	var position = frame.Position;
		//	var tangent = frame.Forward;
		//	var finalUp = frame.Up;
		//	var right = frame.Right;

		//	// Draw tangent vector (forward)
		//	Gizmo.Draw.Color = Color.Red;
		//	Gizmo.Draw.Arrow( position, position + tangent * arrowLength, arrowLength / 10f, arrowLength / 15f );

		//	// Draw up vector (normal)
		//	Gizmo.Draw.Color = Color.Green;
		//	Gizmo.Draw.Arrow( position, position + finalUp * arrowLength, arrowLength / 10f, arrowLength / 15f );

		//	// Draw right vector (binormal)
		//	Gizmo.Draw.Color = Color.Blue;
		//	Gizmo.Draw.Arrow( position, position + right * arrowLength, arrowLength / 10f, arrowLength / 15f );
		//}
	}

	protected override void UpdateObject()
	{
		base.UpdateObject();

		if ( !SceneObject.IsValid() || !Spline.IsValid() )
			return;

		// Set by base call
		var model = SceneObject.Model;

		var mainMaterial = MaterialOverride ?? model.Materials.FirstOrDefault();
		var modelIndices = model.GetIndices();
		var modelVertices = model.GetVertices();

		var (mainMin, mainSize) = GetForwardSpan( model.Bounds, ModelRotation, ModelScale );

		var splineLength = Spline.Spline.Length;

		// Optional cap meshes claim a fixed slice of the spline at each end. Each cap has its
		// own rotation/scale/offset so it can be flipped, resized, or nudged independently.
		bool hasStartCap = StartCap.IsValid() && StartCap != Model.Error && StartCapEnabled;
		bool hasEndCap = EndCap.IsValid() && EndCap != Model.Error && EndCapEnabled;

		float startCapMin = 0f, startCapSize = 0f;
		Vertex[] startCapVerts = null;
		uint[] startCapInds = null;
		Material startCapMaterial = null;
		if ( hasStartCap )
		{
			(startCapMin, startCapSize) = GetForwardSpan( StartCap.Bounds, StartCapRotation, StartCapScale );
			startCapVerts = StartCap.GetVertices();
			startCapInds = StartCap.GetIndices();
			startCapMaterial = StartCap.Materials.FirstOrDefault();
		}

		float endCapMin = 0f, endCapSize = 0f;
		Vertex[] endCapVerts = null;
		uint[] endCapInds = null;
		Material endCapMaterial = null;
		if ( hasEndCap )
		{
			(endCapMin, endCapSize) = GetForwardSpan( EndCap.Bounds, EndCapRotation, EndCapScale );
			endCapVerts = EndCap.GetVertices();
			endCapInds = EndCap.GetIndices();
			endCapMaterial = EndCap.Materials.FirstOrDefault();
		}

		// Interior region for repeating mesh — caps eat into each end.
		float mainStartDistance = MathF.Min( startCapSize, splineLength );
		float mainEndDistance = MathF.Max( mainStartDistance, splineLength - endCapSize );
		float mainLength = mainEndDistance - mainStartDistance;

		var sizeInModelDirWithSpacing = mainSize + Spacing;
		var frameSegments = (int)Math.Ceiling( splineLength / MathF.Max( 1f, mainSize ) );
		if ( frameSegments < 1 ) frameSegments = 1;

		int meshesRequiredWithSpacing = 0;
		float distancePerMeshWitSpacing = sizeInModelDirWithSpacing;
		if ( mainLength > 0f && sizeInModelDirWithSpacing > 0f )
		{
			meshesRequiredWithSpacing = (int)Math.Floor( mainLength / sizeInModelDirWithSpacing );
			if ( FlexFit )
			{
				meshesRequiredWithSpacing = (int)Math.Ceiling( mainLength / sizeInModelDirWithSpacing );
			}

			if ( FlexFit && meshesRequiredWithSpacing > 0 )
			{
				distancePerMeshWitSpacing = mainLength / meshesRequiredWithSpacing;
			}
		}

		int framesPerMesh = 12;
		int frameCount = Math.Max( 2, frameSegments * framesPerMesh + 1 );
		var frames = UseRotationMinimizingFrames
			? CalculateRotationMinimizingTangentFrames( Spline.Spline, frameCount )
			: CalculateTangentFramesUsingUpDir( Spline.Spline, frameCount );

		float endCapRegion = splineLength - mainEndDistance;
		float endCapStartDistance = mainStartDistance + distancePerMeshWitSpacing * meshesRequiredWithSpacing;
		var ordered = new List<Segment>();

		// Spatial order: start cap, main, end cap. Each segment occupies one contiguous slice of
		// the merged index buffer, so the differing-material caps can be peeled off the front/back
		// while the middle stays as the main draw range.
		bool hasStart = hasStartCap && mainStartDistance > 0f && startCapVerts is not null && startCapVerts.Length > 0;
		if ( hasStart )
		{
			ordered.Add( new Segment
			{
				SrcVerts = startCapVerts,
				SrcIndices = startCapInds,
				Material = startCapMaterial,
				DeformRotation = StartCapRotation,
				DeformOffset = StartCapOffset,
				DeformScale = StartCapScale,
				SrcMin = startCapMin,
				SrcSize = startCapSize,
				SegmentStart = 0f,
				DistancePerCopyWithSpacing = mainStartDistance,
				Copies = 1,
				Spacing = 0f,
			} );
		}

		bool hasMain = meshesRequiredWithSpacing > 0;
		if ( hasMain )
		{
			ordered.Add( new Segment
			{
				SrcVerts = modelVertices,
				SrcIndices = modelIndices,
				Material = mainMaterial,
				DeformRotation = ModelRotation,
				DeformOffset = ModelOffset,
				DeformScale = ModelScale,
				SrcMin = mainMin,
				SrcSize = mainSize,
				SegmentStart = mainStartDistance,
				DistancePerCopyWithSpacing = distancePerMeshWitSpacing,
				Copies = meshesRequiredWithSpacing,
				Spacing = Spacing,
			} );
		}

		bool hasEnd = hasEndCap && endCapRegion > 0f && endCapVerts is not null && endCapVerts.Length > 0;
		if ( hasEnd )
		{
			ordered.Add( new Segment
			{
				SrcVerts = endCapVerts,
				SrcIndices = endCapInds,
				Material = endCapMaterial,
				DeformRotation = EndCapRotation,
				DeformOffset = EndCapOffset,
				DeformScale = EndCapScale,
				SrcMin = endCapMin,
				SrcSize = endCapSize,
				SegmentStart = endCapStartDistance,
				DistancePerCopyWithSpacing = endCapRegion,
				Copies = 1,
				Spacing = 0f,
			} );
		}

		if ( ordered.Count == 0 )
			return;

		int totalVerts = 0;
		int totalInds = 0;
		for ( int i = 0; i < ordered.Count; i++ )
		{
			var seg = ordered[i];
			seg.VertexStart = totalVerts;
			seg.IndexStart = totalInds;
			ordered[i] = seg;
			totalVerts += seg.VertexCount;
			totalInds += seg.IndexCount;
		}

		if ( vertexScratch == null || vertexScratch.Length < totalVerts )
			vertexScratch = new Vertex[totalVerts];
		if ( indexScratch == null || indexScratch.Length < totalInds )
			indexScratch = new int[totalInds];

		foreach ( var seg in ordered )
			FillSegment( seg, frames );

		// AddSubMesh is additive with no clear, so rebuild the Mesh each update. The Model builder
		// rebuild below was already per-update, so the marginal cost is just the Mesh handle.
		customMesh = new Mesh();
		customMesh.CreateVertexBuffer( totalVerts, vertexScratch.AsSpan( 0, totalVerts ) );
		customMesh.CreateIndexBuffer( totalInds, indexScratch.AsSpan( 0, totalInds ) );
		customMesh.Bounds = BBox.FromPoints( vertexScratch.Take( totalVerts ).Select( v => v.Position ) );
		customMesh.SetIndexRange( 0, 0 );

		// Peel a differing-material cap off the front/back; what's left in the middle is the main draw.
		int mainStartIndex = 0;
		int mainLastIndex = totalInds;

		if ( hasStart && ordered.First().Material != mainMaterial )
		{
			var startCap = ordered.First();
			customMesh.AddSubMesh( startCap.Material, startCap.IndexStart, startCap.IndexCount );
			mainStartIndex = startCap.IndexStart + startCap.IndexCount;
		}

		if ( hasEnd && ordered.Last().Material != mainMaterial )
		{
			var endCap = ordered.Last();
			customMesh.AddSubMesh( endCap.Material, endCap.IndexStart, endCap.IndexCount );
			mainLastIndex = endCap.IndexStart;
		}

		customMesh.AddSubMesh( mainMaterial, mainStartIndex, mainLastIndex - mainStartIndex );

		customModel = Model.Builder.AddMesh( customMesh ).Create();
		SceneObject.Model = customModel;
	}

	private (float min, float size) GetForwardSpan( BBox bounds, Rotation rotation, Vector3 scale )
	{
		bounds.Mins = bounds.Mins * scale;
		bounds.Maxs = bounds.Maxs * scale;
		bounds = bounds.Rotate( rotation );
		float size = bounds.Size.Dot( Vector3.Forward );
		float min = bounds.Center.Dot( Vector3.Forward ) - size / 2f;
		return (min, size);
	}

	private void FillSegment( Segment seg, Transform[] frames )
	{
		int srcVertsLen = seg.SrcVerts.Length;
		int srcIndsLen = seg.SrcIndices.Length;
		int vBase = seg.VertexStart;
		int iBase = seg.IndexStart;
		var localVerts = vertexScratch;
		var localInds = indexScratch;

		Utility.Parallel.For( 0, seg.Copies, copyIdx =>
		{
			float copyStart = seg.SegmentStart + copyIdx * seg.DistancePerCopyWithSpacing;
			float copyEnd = copyStart + seg.DistancePerCopyWithSpacing - seg.Spacing;

			for ( int i = 0; i < srcVertsLen; i++ )
			{
				var vertex = seg.SrcVerts[i];
				var deformed = vertex;
				Deform( Spline, seg.DeformRotation, seg.DeformOffset, seg.DeformScale,
					vertex.Position, vertex.Normal, vertex.Tangent,
					frames, copyStart, copyEnd, seg.SrcMin, seg.SrcSize,
					out deformed.Position, out deformed.Normal, out deformed.Tangent );
				localVerts[vBase + srcVertsLen * copyIdx + i] = deformed;
			}
			for ( int i = 0; i < srcIndsLen; i++ )
			{
				localInds[iBase + srcIndsLen * copyIdx + i] = (int)(seg.SrcIndices[i] + vBase + srcVertsLen * copyIdx);
			}
		} );
	}

	// TODO Has there ever been a function with more args?
	public static void Deform( SplineComponent spline, Rotation modelRoation, Vector3 modelOffset, Vector3 modelScale, Vector3 localPosition, Vector3 localNormal, Vector4 localTangent, Span<Transform> frames, float startDistance, float endDistance, float minInModelDir, float sizeInModelDir, out Vector3 deformedPosition, out Vector3 deformedNormal, out Vector4 deformedTangent )
	{
		// rotate localPosition by model rotation
		localPosition = modelRoation * (localPosition * modelScale);

		// Map localPosition.x to t along the spline segment
		float t = (localPosition.x - minInModelDir) / sizeInModelDir;
		t = Math.Clamp( t, 0f, 1f );

		float distanceAlongSpline = MathX.Lerp( startDistance, endDistance, t );

		// Calculate the frame index and interpolation factor
		float frameFloatIndex = (distanceAlongSpline / spline.Spline.Length) * (frames.Length - 1);
		int frameIndex = Math.Clamp( (int)Math.Floor( frameFloatIndex ), 0, frames.Length - 2 );
		float frameT = Math.Clamp( frameFloatIndex - frameIndex, 0f, 1f );

		Transform frame0 = frames[frameIndex];
		Transform frame1 = frames[frameIndex + 1];

		Vector3 position = Vector3.Lerp( frame0.Position, frame1.Position, frameT );
		Rotation rotation = Rotation.Slerp( frame0.Rotation, frame1.Rotation, frameT );

		// Interpolate scale from frames
		Vector3 scale0 = frame0.Scale;
		Vector3 scale1 = frame1.Scale;
		Vector3 scale = Vector3.Lerp( scale0, scale1, frameT );

		// Offset is applied in the per-frame local space so it rotates with the spline.
		// x  shifts the mesh along the local tangent (phase along the curve),
		// y/z shift perpendicular to the tangent so the mesh follows a parallel curve.
		Vector3 scaledLocalPosition = new(
			modelOffset.x,
			(localPosition.y + modelOffset.y) * scale.y,
			(localPosition.z + modelOffset.z) * scale.z );

		// Apply model rotation and local offsets
		deformedPosition = position + rotation * scaledLocalPosition;

		deformedNormal = rotation * (modelRoation * localNormal);
		deformedTangent = new Vector4( rotation * (modelRoation * localTangent), localTangent.w );
	}

	// Internal for now no need to expose this yet without, spline deformers
	internal static Transform[] CalculateTangentFramesUsingUpDir( Spline spline, int frameCount )
	{
		Transform[] frames = new Transform[frameCount];

		float totalSplineLength = spline.Length;

		var sample = spline.SampleAtDistance( 0f );
		sample.Up = Vector3.Up;

		// Choose an initial up vector if tangent is parallel to Up
		if ( MathF.Abs( Vector3.Dot( sample.Tangent, sample.Up ) ) > 0.999f )
		{
			sample.Up = Vector3.Right;
		}

		for ( int i = 0; i < frameCount; i++ )
		{
			float t = (float)i / (frameCount - 1);
			float distance = t * totalSplineLength;

			sample = spline.SampleAtDistance( distance );

			// Apply roll
			var newUp = Rotation.FromAxis( sample.Tangent, sample.Roll ) * sample.Up;

			Rotation rotation = Rotation.LookAt( sample.Tangent, newUp );

			frames[i] = new Transform( sample.Position, rotation, sample.Scale );
		}

		return frames;
	}

	// Internal for now no need to expose this yet without spline deformers
	internal static Transform[] CalculateRotationMinimizingTangentFrames( Spline spline, int frameCount )
	{
		Transform[] frames = new Transform[frameCount];

		float totalSplineLength = spline.Length;

		// Initialize the up vector
		var previousSample = spline.SampleAtDistance( 0f );
		Vector3 up = Vector3.Up;

		// Choose an initial up vector if tangent is parallel to Up
		if ( MathF.Abs( Vector3.Dot( previousSample.Tangent, up ) ) > 0.999f )
		{
			up = Vector3.Right;
		}

		up = Rotation.FromAxis( previousSample.Tangent, previousSample.Roll ) * up;

		frames[0] = new Transform( previousSample.Position, Rotation.LookAt( previousSample.Tangent, up ), previousSample.Scale );

		for ( int i = 1; i < frameCount; i++ )
		{
			float t = (float)i / (frameCount - 1);
			float distance = t * totalSplineLength;

			var sample = spline.SampleAtDistance( distance );

			// Parallel transport the up vector
			up = GetRotationMinimizingNormal( previousSample.Position, previousSample.Tangent, up, sample.Position, sample.Tangent );

			// Apply roll
			float deltaRoll = sample.Roll - previousSample.Roll;
			up = Rotation.FromAxis( sample.Tangent, deltaRoll ) * up;

			Rotation rotation = Rotation.LookAt( sample.Tangent, up );

			frames[i] = new Transform( sample.Position, rotation, sample.Scale );

			previousSample = sample;
		}

		// Correct up vectors for looped splines
		if ( spline.IsLoop && frames.Length > 1 )
		{
			Vector3 startUp = frames[0].Rotation.Up;
			Vector3 endUp = frames[^1].Rotation.Up;

			float theta = MathF.Acos( Vector3.Dot( startUp, endUp ) ) / (frames.Length - 1);
			if ( Vector3.Dot( frames[0].Rotation.Forward, Vector3.Cross( startUp, endUp ) ) > 0 )
			{
				theta = -theta;
			}

			for ( int i = 0; i < frames.Length; i++ )
			{
				Rotation R = Rotation.FromAxis( frames[i].Rotation.Forward, (theta * i).RadianToDegree() );
				Vector3 correctedUp = R * frames[i].Rotation.Up;
				frames[i] = new Transform( frames[i].Position, Rotation.LookAt( frames[i].Rotation.Forward, correctedUp ), frames[i].Scale );
			}
		}

		return frames;
	}

	private static Vector3 GetRotationMinimizingNormal( Vector3 posA, Vector3 tangentA, Vector3 normalA, Vector3 posB, Vector3 tangentB )
	{
		// Source: https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/Computation-of-rotation-minimizing-frames.pdf
		Vector3 v1 = posB - posA;
		float v1DotV1Half = Vector3.Dot( v1, v1 ) / 2f;
		float r1 = Vector3.Dot( v1, normalA ) / v1DotV1Half;
		float r2 = Vector3.Dot( v1, tangentA ) / v1DotV1Half;
		Vector3 nL = normalA - r1 * v1;
		Vector3 tL = tangentA - r2 * v1;
		Vector3 v2 = tangentB - tL;
		float r3 = Vector3.Dot( v2, nL ) / Vector3.Dot( v2, v2 );
		return (nL - 2f * r3 * v2).Normal;
	}
}

