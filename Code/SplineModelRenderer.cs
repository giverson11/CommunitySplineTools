using System;

namespace Sandbox;

public sealed class SplineModelRenderer : ModelRenderer
{
	[Property, Category( "Spline" )] public SplineComponent Spline { get; set; }

	[Property, Category( "Spline" )]
	public Rotation ModelRotation
	{
		get => _modelRotation;
		set
		{
			_modelRotation = value;
			UpdateObject();
		}
	}

	private Rotation _modelRotation = Rotation.Identity;

	[Property, Category( "Spline" )]
	public Vector3 ModelScale
	{
		get => _modelScale;
		set
		{
			_modelScale = value;
			UpdateObject();
		}
	}

	private Vector3 _modelScale = Vector3.One;

	[Property, Category( "Spline" )]
	public Vector3 ModelOffset
	{
		get => _modelOffset;
		set
		{
			_modelOffset = value;
			UpdateObject();
		}
	}

	private Vector3 _modelOffset = Vector3.Zero;

	[Property, Category( "Spline" )]
	public bool UseRotationMinimizingFrames
	{
		get => _useRotationMinimizingFrames;
		set
		{
			_useRotationMinimizingFrames = value;
			UpdateObject();
		}
	}

	private bool _useRotationMinimizingFrames = true;

	private Mesh customMesh = new();
	private Model customModel = Model.Error;

	private Vertex[] modelVertices = null;
	private uint[] modelIndices = null;

	private Vertex[] deformedVertices;
	private int[] deformedIndices;

	[Property, Category( "Spline" ), MinMax(0, float.PositiveInfinity)]
	public float Spacing
	{
		get => _spacing;
		set
		{
			_spacing = value;
			UpdateObject();
		}
	}

	private float _spacing = 0f;

	[Property, Category( "Spline" )]
	public bool FlexFit
	{
		get => _flexFit;
		set
		{
			_flexFit = value;
			UpdateObject();
		}
	}

	private bool _flexFit = false;

	[Property, FeatureEnabled( "Start Cap" )]
	public bool startCapEnabled
	{
		get; set
		{
			field = value;
			UpdateObject();
		}
	}

	[Property, FeatureEnabled( "End Cap" )]
	public bool endCapEnabled
	{
		get; set
		{
			field = value;
			UpdateObject();
		}
	}

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Model StartCap
	{
		get => _startCap;
		set
		{
			_startCap = value;
			UpdateObject();
		}
	}

	private Model _startCap;

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Rotation StartCapRotation
	{
		get => _startCapRotation;
		set
		{
			_startCapRotation = value;
			UpdateObject();
		}
	}

	private Rotation _startCapRotation = Rotation.Identity;

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Vector3 StartCapScale
	{
		get => _startCapScale;
		set
		{
			_startCapScale = value;
			UpdateObject();
		}
	}

	private Vector3 _startCapScale = Vector3.One;

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Vector3 StartCapOffset
	{
		get => _startCapOffset;
		set
		{
			_startCapOffset = value;
			UpdateObject();
		}
	}

	private Vector3 _startCapOffset = Vector3.Zero;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Model EndCap
	{
		get => _endCap;
		set
		{
			_endCap = value;
			UpdateObject();
		}
	}

	private Model _endCap;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Rotation EndCapRotation
	{
		get => _endCapRotation;
		set
		{
			_endCapRotation = value;
			UpdateObject();
		}
	}

	private Rotation _endCapRotation = Rotation.Identity;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Vector3 EndCapScale
	{
		get => _endCapScale;
		set
		{
			_endCapScale = value;
			UpdateObject();
		}
	}

	private Vector3 _endCapScale = Vector3.One;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Vector3 EndCapOffset
	{
		get => _endCapOffset;
		set
		{
			_endCapOffset = value;
			UpdateObject();
		}
	}

	private Vector3 _endCapOffset = Vector3.Zero;

	private Mesh startCapMesh;
	private Mesh endCapMesh;

	private Vertex[] startCapDeformedVertices;
	private int[] startCapDeformedIndices;
	private Vertex[] endCapDeformedVertices;
	private int[] endCapDeformedIndices;

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

		if ( customMesh == null )
		{
			customMesh = new();
		}

		if ( !SceneObject.IsValid() || !Spline.IsValid() )
			return;

		// Set by base call
		var model = SceneObject.Model;

		var mainMaterial = MaterialOverride ?? model.Materials.FirstOrDefault();
		modelIndices = model.GetIndices();
		modelVertices = model.GetVertices();

		var (mainMin, mainSize) = GetForwardSpan( model.Bounds, ModelRotation, ModelScale );

		var splineLength = Spline.Spline.Length;

		// Optional cap meshes claim a fixed slice of the spline at each end. Each cap has its
		// own rotation/scale/offset so it can be flipped, resized, or nudged independently.
		bool hasStartCap = _startCap.IsValid() && _startCap != Model.Error && startCapEnabled;
		bool hasEndCap = _endCap.IsValid() && _endCap != Model.Error && endCapEnabled;

		float startCapMin = 0f, startCapSize = 0f;
		Vertex[] startCapVerts = null;
		uint[] startCapInds = null;
		if ( hasStartCap )
		{
			(startCapMin, startCapSize) = GetForwardSpan( _startCap.Bounds, StartCapRotation, StartCapScale );
			startCapVerts = _startCap.GetVertices();
			startCapInds = _startCap.GetIndices();
		}

		float endCapMin = 0f, endCapSize = 0f;
		Vertex[] endCapVerts = null;
		uint[] endCapInds = null;
		if ( hasEndCap )
		{
			(endCapMin, endCapSize) = GetForwardSpan( _endCap.Bounds, EndCapRotation, EndCapScale );
			endCapVerts = _endCap.GetVertices();
			endCapInds = _endCap.GetIndices();
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

		// Main repeating mesh in the interior region.
		bool builtMain = false;
		if ( meshesRequiredWithSpacing > 0 )
		{
			BuildSegmentMesh( customMesh,
				modelVertices, modelIndices, mainMaterial,
				ModelRotation, ModelOffset, ModelScale,
				mainMin, mainSize,
				mainStartDistance, distancePerMeshWitSpacing,
				meshesRequiredWithSpacing, Spacing,
				frames,
				ref deformedVertices, ref deformedIndices );
			builtMain = true;
		}

		// Start cap occupies [0, mainStartDistance].
		bool builtStartCap = false;
		if ( hasStartCap && mainStartDistance > 0f )
		{
			startCapMesh ??= new();
			BuildSegmentMesh( startCapMesh,
				startCapVerts, startCapInds,
				MaterialOverride ?? _startCap.Materials.FirstOrDefault(),
				StartCapRotation, StartCapOffset, StartCapScale,
				startCapMin, startCapSize,
				0f, mainStartDistance,
				1, 0f,
				frames,
				ref startCapDeformedVertices, ref startCapDeformedIndices );
			builtStartCap = true;
		}

		// End cap occupies [mainEndDistance, splineLength].
		bool builtEndCap = false;
		float endCapRegion = splineLength - mainEndDistance;
		if ( hasEndCap && endCapRegion > 0f )
		{
			endCapMesh ??= new();
			BuildSegmentMesh( endCapMesh,
				endCapVerts, endCapInds,
				MaterialOverride ?? _endCap.Materials.FirstOrDefault(),
				EndCapRotation, EndCapOffset, EndCapScale,
				endCapMin, endCapSize,
				mainEndDistance, endCapRegion,
				1, 0f,
				frames,
				ref endCapDeformedVertices, ref endCapDeformedIndices );
			builtEndCap = true;
		}

		if ( !builtMain && !builtStartCap && !builtEndCap )
			return;

		var builder = Model.Builder;
		if ( builtMain ) builder = builder.AddMesh( customMesh );
		if ( builtStartCap ) builder = builder.AddMesh( startCapMesh );
		if ( builtEndCap ) builder = builder.AddMesh( endCapMesh );

		customModel = builder.Create();
		// TODO use modelsystem.ChangeModel
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

	private void BuildSegmentMesh(
		Mesh mesh,
		Vertex[] srcVerts,
		uint[] srcIndices,
		Material material,
		Rotation deformRotation,
		Vector3 deformOffset,
		Vector3 deformScale,
		float srcMin,
		float srcSize,
		float segmentStart,
		float distancePerCopyWithSpacing,
		int copies,
		float spacing,
		Transform[] frames,
		ref Vertex[] vertScratch,
		ref int[] indexScratch )
	{
		int totalVerts = srcVerts.Length * copies;
		int totalInds = srcIndices.Length * copies;

		if ( vertScratch == null || vertScratch.Length < totalVerts )
			vertScratch = new Vertex[totalVerts];
		if ( indexScratch == null || indexScratch.Length < totalInds )
			indexScratch = new int[totalInds];

		var localVerts = vertScratch;
		var localInds = indexScratch;

		Utility.Parallel.For( 0, copies, copyIdx =>
		{
			float copyStart = segmentStart + copyIdx * distancePerCopyWithSpacing;
			float copyEnd = copyStart + distancePerCopyWithSpacing - spacing;

			for ( int i = 0; i < srcVerts.Length; i++ )
			{
				var vertex = srcVerts[i];
				var deformedVertex = vertex;
				Deform( Spline, deformRotation, deformOffset, deformScale,
					vertex.Position, vertex.Normal, vertex.Tangent,
					frames, copyStart, copyEnd, srcMin, srcSize,
					out deformedVertex.Position, out deformedVertex.Normal, out deformedVertex.Tangent );
				localVerts[srcVerts.Length * copyIdx + i] = deformedVertex;
			}
			for ( int i = 0; i < srcIndices.Length; i++ )
			{
				localInds[srcIndices.Length * copyIdx + i] = (int)(srcIndices[i] + srcVerts.Length * copyIdx);
			}
		} );

		mesh.Material = material;

		if ( mesh.HasVertexBuffer )
		{
			if ( mesh.IndexCount < totalInds )
				mesh.SetIndexBufferSize( indexScratch.Length );
			mesh.SetIndexBufferData( indexScratch.AsSpan( 0, totalInds ) );
			mesh.SetIndexRange( 0, totalInds );

			if ( mesh.VertexCount < totalVerts )
				mesh.SetVertexBufferSize( vertScratch.Length );
			mesh.SetVertexRange( 0, totalVerts );
			mesh.SetVertexBufferData( vertScratch.AsSpan( 0, totalVerts ) );
		}
		else
		{
			mesh.CreateVertexBuffer( totalVerts, Vertex.Layout, vertScratch.AsSpan( 0, totalVerts ) );
			mesh.CreateIndexBuffer( totalInds, indexScratch.AsSpan( 0, totalInds ) );
		}

		mesh.Bounds = BBox.FromPoints( vertScratch.Take( totalVerts ).Select( v => v.Position ) );
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
		Vector3 scaledLocalPosition = new Vector3(
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
	internal static  Transform[] CalculateRotationMinimizingTangentFrames( Spline spline, int frameCount )
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

