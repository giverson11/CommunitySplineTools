using System;
using System.Numerics;

namespace Sandbox;

public sealed class SplineCollider : ModelCollider, Component.ExecuteInEditor
{
	[Property, Category( "Spline" )] public SplineComponent Spline 
	{ 
		get; 
		set
		{
			field?.Spline.SplineChanged -= MarkDirty;
			field = value;
			if(Enabled) {
				field.Spline.SplineChanged += MarkDirty;
				Rebuild();
			}
		} 
	}

	[Property, Category("Spline")]
	[Range( 0, 16 )]
	public int Subdivision
	{
		get; 
		set
		{
			field = value;
			Rebuild();
		}
	} = 0;

	[Property, Category( "Spline" )]
	public Rotation ModelRotation
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Rotation.Identity;

	[Property, Category( "Spline" )]
	public Vector3 ModelScale
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Vector3.One;


	[Property, Category( "Spline" )]
	public Vector3 ModelOffset
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Vector3.Zero;


	private Vector3 ModelForward => ModelRotation.Forward;

	[Property, Category( "Spline" )]
	public bool UseRotationMinimizingFrames
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = false;



	private bool IsDirty
	{
		get; set;
	} = true;


	[Property, Category( "Spline" ), MinMax( 0, float.PositiveInfinity )]
	public float Spacing
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = 0f;


	[Property, Category( "Spline" )]
	public bool FlexFit
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = false;


	// Optional cap colliders that claim a fixed slice of the spline at each end, mirroring
	// the cap feature on SplineModelRenderer. Each cap is its own physics Model with its own
	// rotation/scale/offset so it can be flipped, resized, or nudged independently.

	[Property, FeatureEnabled( "Start Cap" )]
	public bool StartCapEnabled
	{
		get;
		set
		{
			field = value;
			Rebuild();
		}
	}

	[Property, FeatureEnabled( "End Cap" )]
	public bool EndCapEnabled
	{
		get;
		set
		{
			field = value;
			Rebuild();
		}
	}

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Model StartCap
	{
		get;
		set
		{
			field = value;
			Rebuild();
		}
	}

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Rotation StartCapRotation
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Rotation.Identity;

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Vector3 StartCapScale
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Vector3.One;

	[Property, Category( "Spline" ), Feature( "Start Cap" )]
	public Vector3 StartCapOffset
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Vector3.Zero;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Model EndCap
	{
		get;
		set
		{
			field = value;
			Rebuild();
		}
	}

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Rotation EndCapRotation
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Rotation.Identity;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Vector3 EndCapScale
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Vector3.One;

	[Property, Category( "Spline" ), Feature( "End Cap" )]
	public Vector3 EndCapOffset
	{
		get;
		set
		{
			field = value;
			IsDirty = true;
		}
	} = Vector3.Zero;


	protected override void OnEnabled()
	{
		if ( Model.IsValid() && Spline.IsValid() )
		{
			IsDirty = true;
			Spline.Spline.SplineChanged += MarkDirty;
		}
		base.OnEnabled();
	}

	private void MarkDirty()
	{
		IsDirty = true;
	}

	protected override void OnDisabled()
	{
		Spline.Spline.SplineChanged -= MarkDirty;
		subHulls.Clear();
		subMeshes.Clear();
		startCapSubHulls.Clear();
		startCapSubMeshes.Clear();
		endCapSubHulls.Clear();
		endCapSubMeshes.Clear();
		base.OnDisabled();
	}

	protected override void OnUpdate()
	{
		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		if ( !IsDirty )
		{
			return;
		}

		// Nothing to deform
		if ( subHulls.Count == 0 && subMeshes.Count == 0
			&& startCapSubHulls.Count == 0 && startCapSubMeshes.Count == 0
			&& endCapSubHulls.Count == 0 && endCapSubMeshes.Count == 0 )
		{
			return;
		}

		UpdateCollisions();
	}

	// This is internal hack it for now
	private PhysicsBody _PhysicsBody => Rigidbody.IsValid() ? Rigidbody.PhysicsBody : KeyframeBody;

	private BBox? _physicPartBounds = null;

	private void UpdateCollisions()
	{
		// Ensure we have valid physics bounds
		if ( _physicPartBounds is null )
			return;

		var (mainMin, mainSize) = GetForwardSpan( Model.Bounds, ModelRotation, ModelScale );

		var splineLength = Spline.Spline.Length;

		// Caps eat a fixed slice off each end; the repeating mesh fills the interior between them.
		bool hasStartCap = StartCapEnabled && (startCapSubHulls.Count > 0 || startCapSubMeshes.Count > 0);
		bool hasEndCap = EndCapEnabled && (endCapSubHulls.Count > 0 || endCapSubMeshes.Count > 0);

		float startCapMin = 0f, startCapSize = 0f;
		if ( hasStartCap )
			(startCapMin, startCapSize) = GetForwardSpan( StartCap.Bounds, StartCapRotation, StartCapScale );

		float endCapMin = 0f, endCapSize = 0f;
		if ( hasEndCap )
			(endCapMin, endCapSize) = GetForwardSpan( EndCap.Bounds, EndCapRotation, EndCapScale );

		// Interior region for the repeating mesh — caps eat into each end.
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

		// Calculate frames along the spline
		int framesPerMesh = 12; // Adjust as needed
		int frameCount = Math.Max( 2, frameSegments * framesPerMesh + 1 );

		var frames = UseRotationMinimizingFrames
			? SplineModelRenderer.CalculateRotationMinimizingTangentFrames( Spline.Spline, frameCount )
			: SplineModelRenderer.CalculateTangentFramesUsingUpDir( Spline.Spline, frameCount );


		// Clear existing shapes
		_PhysicsBody.ClearShapes();

		// Main repeating shapes in the interior region.
		for ( var meshIndex = 0; meshIndex < meshesRequiredWithSpacing; meshIndex++ )
		{
			float startDistance = mainStartDistance + meshIndex * distancePerMeshWitSpacing;
			float endDistance = startDistance + distancePerMeshWitSpacing - Spacing;

			DeformAndAddShapes( ref subMeshes, ref subHulls, ModelRotation, ModelOffset, ModelScale, mainMin, mainSize, frames, startDistance, endDistance );
		}

		// Start cap occupies [0, mainStartDistance].
		if ( hasStartCap && mainStartDistance > 0f )
		{
			DeformAndAddShapes( ref startCapSubMeshes, ref startCapSubHulls, StartCapRotation, StartCapOffset, StartCapScale, startCapMin, startCapSize, frames, 0f, mainStartDistance );
		}

		// End cap occupies [endCapStartDistance, splineLength].
		float endCapRegion = splineLength - mainEndDistance;
		float endCapStartDistance = mainStartDistance + distancePerMeshWitSpacing * meshesRequiredWithSpacing;
		if ( hasEndCap && endCapRegion > 0f )
		{
			DeformAndAddShapes( ref endCapSubMeshes, ref endCapSubHulls, EndCapRotation, EndCapOffset, EndCapScale, endCapMin, endCapSize, frames, endCapStartDistance, endCapStartDistance + endCapRegion );
		}

		IsDirty = false;
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

	// Deforms a set of sub-shapes across [startDistance, endDistance] of the spline and adds the
	// resulting physics shapes to the body. Shared by the repeating main mesh and both caps.
	private void DeformAndAddShapes( ref List<SubMesh> meshes, ref List<SubHull> hulls, Rotation rotation, Vector3 offset, Vector3 scale, float min, float size, Transform[] frames, float startDistance, float endDistance )
	{
		foreach ( var subMesh in meshes )
		{
			var deformedVertices = new List<Vector3>( subMesh.Vertices.Count );
			foreach ( var vertex in subMesh.Vertices )
			{
				SplineModelRenderer.Deform( Spline, rotation, offset, scale, vertex, Vector3.Up, new Vector4( Vector3.Up, 0f ), frames, startDistance, endDistance, min, size, out var deformedVertex, out _, out _ );
				deformedVertices.Add( deformedVertex );
			}
			var shape = _PhysicsBody.AddMeshShape( deformedVertices, subMesh.Indices );
			ApplyShapeProperties( shape, subMesh.Surface, startDistance, endDistance );
		}

		foreach ( var subHull in hulls )
		{
			var deformedVertices = new List<Vector3>( subHull.Vertices.Count );
			foreach ( var vertex in subHull.Vertices )
			{
				SplineModelRenderer.Deform( Spline, rotation, offset, scale, vertex, Vector3.Up, new Vector4( Vector3.Up, 0f ), frames, startDistance, endDistance, min, size, out var deformedVertex, out _, out _ );
				deformedVertices.Add( deformedVertex );
			}
			var shape = _PhysicsBody.AddHullShape( Vector3.Zero, Rotation.Identity, deformedVertices );
			ApplyShapeProperties( shape, subHull.Surface, startDistance, endDistance );
		}
	}

	private void ApplyShapeProperties( PhysicsShape shape, Surface surface, float startDistance, float endDistance )
	{
		// SurfaceVelocity is body-local, so align it with the spline tangent at the middle of this
		// shape's span and undo the body's world rotation.
		var midDistance = (startDistance + endDistance) * 0.5f;
		var tangent = Spline.Spline.SampleAtDistance( midDistance ).Tangent.Normal;
		Rotation rot = Rotation.FromToRotation(SurfaceVelocity.Normal, tangent);
		shape.SurfaceVelocity = rot * SurfaceVelocity;

		shape.Surface = surface;
		if ( Friction != null ) shape.Friction = (float)Friction;
		if ( Elasticity != null ) shape.Surface.Elasticity = (float)Elasticity;
		if ( RollingResistance != null ) shape.Surface.RollingResistance = (float)RollingResistance;
	}


	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody, Transform local )
	{
		if ( Model is null || Model.Physics is null )
			yield break;

		if ( Model.Physics.Parts.Count == 0 )
			yield break;

		if ( Spline is null )
			yield break;

		subMeshes.Clear();
		subHulls.Clear();
		startCapSubMeshes.Clear();
		startCapSubHulls.Clear();
		endCapSubMeshes.Clear();
		endCapSubHulls.Clear();

		_physicPartBounds = null;

		BuildSubShapes( Model, local, ModelRotation, subHulls, subMeshes, ref _physicPartBounds );

		if ( StartCapEnabled && StartCap.IsValid() && StartCap != Model.Error )
		{
			BBox? capBounds = null;
			BuildSubShapes( StartCap, local, StartCapRotation, startCapSubHulls, startCapSubMeshes, ref capBounds );
		}

		if ( EndCapEnabled && EndCap.IsValid() && EndCap != Model.Error )
		{
			BBox? capBounds = null;
			BuildSubShapes( EndCap, local, EndCapRotation, endCapSubHulls, endCapSubMeshes, ref capBounds );
		}

		// Body-level properties only come from the main model's parts.
		foreach ( var part in Model.Physics.Parts )
		{
			if ( part.Mass > 0 )
				targetBody.Mass = part.Mass;

			if ( part.OverrideMassCenter )
				targetBody.LocalMassCenter = part.MassCenterOverride;

			if ( part.LinearDamping > 0 )
				targetBody.LinearDamping = part.LinearDamping;

			if ( part.AngularDamping > 0 )
				targetBody.AngularDamping = part.AngularDamping;
		}

		UpdateCollisions();
	}

	// Subdivides every physics part of a model into deformable sub-hulls/sub-meshes appended to the
	// given lists. Used for the main model and each cap so they share the same tessellation path.
	private void BuildSubShapes( Model model, Transform local, Rotation modelRotation, List<SubHull> targetHulls, List<SubMesh> targetMeshes, ref BBox? bounds )
	{
		if ( model is null || model.Physics is null )
			return;

		foreach ( var part in model.Physics.Parts )
		{
			// Bone transform
			var bx = local.ToWorld( part.Transform );

			foreach ( var sphere in part.Spheres )
			{
				const int rings = 8;
				SubdivideSphere( rings, sphere.Sphere.Center, sphere.Sphere.Radius, sphere.Surface, bx, modelRotation.Forward, targetHulls );

				var sphereBounds = new BBox( sphere.Sphere.Center - new Vector3( sphere.Sphere.Radius ), sphere.Sphere.Center + new Vector3( sphere.Sphere.Radius ) );

				sphereBounds = sphereBounds.Transform( bx );

				bounds = bounds?.AddBBox( sphereBounds ) ?? sphereBounds;
			}

			foreach ( var capsule in part.Capsules )
			{
				var rotatedCenterA = bx.PointToWorld( capsule.Capsule.CenterA );
				var rotatedCenterB = bx.PointToWorld( capsule.Capsule.CenterB );
				SubdivideCapsule( 4 + Subdivision, rotatedCenterA, rotatedCenterB, capsule.Capsule.Radius, capsule.Surface, targetHulls );

				var capsuleBounds = BBox.FromPoints(
					[
						rotatedCenterA - new Vector3( capsule.Capsule.Radius ),
						rotatedCenterA + new Vector3( capsule.Capsule.Radius ),
						rotatedCenterB - new Vector3( capsule.Capsule.Radius ),
						rotatedCenterB + new Vector3( capsule.Capsule.Radius )
					] );

				bounds = bounds?.AddBBox( capsuleBounds ) ?? capsuleBounds;
			}

			foreach ( var hull in part.Hulls )
			{
				SubdivideHull( hull, Subdivision, hull.Surface, bx, targetHulls );

				var hullBounds = hull.Bounds.Transform( bx ).Rotate( modelRotation );

				bounds = bounds?.AddBBox( hullBounds ) ?? hullBounds;
			}

			foreach ( var mesh in part.Meshes )
			{
				SubdivideMesh( mesh, bx, targetMeshes );

				var meshBounds = mesh.Bounds.Transform( bx );

				bounds = bounds?.AddBBox( meshBounds ) ?? meshBounds;
			}
		}
	}

	private void SubdivideMesh( PhysicsGroupDescription.BodyPart.MeshPart mesh, Transform transform, List<SubMesh> target )
	{
		var triangles = mesh.GetTriangles().ToList();

		// TODO slow as fuck can be improved by getting the lists directly rather than the traingle objects
		// can be done once we are out of scene staging.
		var vertices = new List<Vector3>();
		var indices = new List<int>( triangles.Count * 3 );
		var vertexMap = new Dictionary<Vector3, int>( new Vector3Comparer() );

		foreach ( var triangle in triangles )
		{
			if ( !vertexMap.ContainsKey( triangle.A ) )
			{
				vertexMap[triangle.A] = vertices.Count;
				vertices.Add( triangle.A );
			}
			indices.Add( vertexMap[triangle.A] );

			if ( !vertexMap.ContainsKey( triangle.B ) )
			{
				vertexMap[triangle.B] = vertices.Count;
				vertices.Add( triangle.B );
			}
			indices.Add( vertexMap[triangle.B] );

			if ( !vertexMap.ContainsKey( triangle.C ) )
			{
				vertexMap[triangle.C] = vertices.Count;
				vertices.Add( triangle.C );
			}
			indices.Add( vertexMap[triangle.C] );
		}

		target.Add( new SubMesh
		{
			Vertices = vertices,
			Indices = indices,
			Surface = mesh.Surface,
			PartTransform = transform
		} );
	}

	private void SubdivideHull( PhysicsGroupDescription.BodyPart.HullPart hull, int ringCount, Surface surface, Transform transform, List<SubHull> target )
	{

		// Transform all the hull vertices once
		// TODO can optimzie this all by getting vertices and lines directly from hull an transforming than once
		// instead of this LINQ madness, will do once out of scenestaging.
		List<Vector3> transformedVertices = hull.GetPoints().Select( vertex => transform.PointToWorld( vertex ) ).ToList();
		if ( ringCount == 0 )
		{
			target.Add( new SubHull
			{
				Vertices = transformedVertices,
				Surface = surface,
			} );
			return;
		}

		// Get the transformed edges
		var transformedLines = hull.GetLines().Select( line => new Line(
		transform.PointToWorld( line.Start ) ,
		transform.PointToWorld( line.End ) )).ToList();

		// Project all vertices along Vector3.Forward
		float minProj = transformedVertices.Min( vertex => Vector3.Dot( vertex, Vector3.Forward ) );
		float maxProj = transformedVertices.Max( vertex => Vector3.Dot( vertex, Vector3.Forward ) );

		float sizeInModelDir = maxProj - minProj;
		float interval = sizeInModelDir / ringCount;

		const float tolerance = 0.01f;

		List<List<Vector3>> rings = new List<List<Vector3>>();

		// Create rings and intersect with existing edges
		for ( int i = 0; i <= ringCount; i++ )
		{
			float ringProj = minProj + (i * interval);
			var ringVertices = new HashSet<Vector3>( new Vector3Comparer() );

			// Find intersections with existing edges
			foreach ( var line in transformedLines )
			{
				float startProj = Vector3.Dot( line.Start, Vector3.Forward );
				float endProj = Vector3.Dot( line.End, Vector3.Forward );

				// Check if line crosses this ring plane
				if ( (startProj <= ringProj + tolerance && endProj >= ringProj - tolerance) ||
					 (endProj <= ringProj + tolerance && startProj >= ringProj - tolerance) )
				{
					// Calculate intersection point
					float t = (ringProj - startProj) / (endProj - startProj);
					Vector3 intersection = Vector3.Lerp( line.Start, line.End, t );
					ringVertices.Add( intersection );
				}
			}

			if ( ringVertices.Count > 0 )
			{
				rings.Add( ringVertices.ToList() );
			}
		}

		// Group rings into SubHulls
		for ( int i = 0; i < rings.Count - 1; i++ )
		{
			var currentGroup = new List<Vector3>();

			// Add current ring
			currentGroup.AddRange( rings[i] );

			// Add next ring
			currentGroup.AddRange( rings[i + 1] );

			// Find and add original vertices that fall between these rings
			float groupStart = rings[i].Min( v => Vector3.Dot( v, Vector3.Forward ) );
			float groupEnd = rings[i + 1].Max( v => Vector3.Dot( v, Vector3.Forward ) );

			foreach ( var vertex in transformedVertices )
			{
				float vertexProj = Vector3.Dot( vertex, Vector3.Forward );

				if ( vertexProj >= groupStart - tolerance && vertexProj <= groupEnd + tolerance )
				{
					currentGroup.Add( vertex );
				}
			}

			target.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
			} );
		}
	}


	private void SubdivideSphere( int rings, Vector3 center, float radius, Surface surface, Transform transform, Vector3 modelForward, List<SubHull> target )
	{
		var ringPoints = new List<Vector3>[rings];

		Vector3 direction = modelForward;

		// Find two vectors orthogonal to ModelForward to form a coordinate system
		// TODO there are better ways todo this
		Vector3 right = Vector3.Cross( direction, Vector3.Up );
		if ( right.Length < 0.001f )
		{
			right = Vector3.Cross( direction, Vector3.Right );
		}
		right = right.Normal;
		Vector3 up = Vector3.Cross( right, direction ).Normal;

		for ( int i = 0; i < rings; ++i )
		{
			ringPoints[i] = new List<Vector3>();
			float v = i / (float)(rings - 1);
			float theta = v * MathF.PI; // Angle from 0 to PI

			float sinTheta = MathF.Sin( theta );
			float cosTheta = MathF.Cos( theta );

			for ( int j = 0; j < rings; ++j )
			{
				float u = j / (float)(rings - 1);
				float phi = u * 2.0f * MathF.PI;

				float sinPhi = MathF.Sin( phi );
				float cosPhi = MathF.Cos( phi );

				// Convert spherical coordinates to Cartesian coordinates aligned along ModelForward
				Vector3 point = center
					+ (right * (sinTheta * cosPhi * radius))
					+ (up * (sinTheta * sinPhi * radius))
					+ (direction * (cosTheta * radius));

				ringPoints[i].Add( transform.PointToWorld( point ) );
			}
		}

		for ( int i = 0; i < rings - 1; ++i )
		{
			var currentGroup = new List<Vector3>();
			currentGroup.AddRange( ringPoints[i] );
			currentGroup.AddRange( ringPoints[i + 1] );

			target.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
			} );
		}
	}

	private void SubdivideCapsule( int totalRings, Vector3 centerA, Vector3 centerB, float radius, Surface surface, List<SubHull> target )
	{
		const int segments = 8;
		if ( totalRings < 4 )
		{
			throw new ArgumentException( "Number of rings must be at least 4 to form a valid capsule." );
		}

		// Generate all rings along the capsule
		List<List<Vector3>> ringPoints = new();
		GenerateCapsuleRings( centerA, centerB, radius, totalRings, segments, ringPoints );

		// Group the rings into SubHulls
		for ( int i = 0; i < ringPoints.Count - 1; i++ )
		{
			var currentGroup = new List<Vector3>();
			currentGroup.AddRange( ringPoints[i] );
			currentGroup.AddRange( ringPoints[i + 1] );

			target.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
			} );
		}
	}

	private void GenerateCapsuleRings( Vector3 centerA, Vector3 centerB, float radius, int totalRings, int segments, List<List<Vector3>> ringPoints )
	{
		Vector3 direction = (centerB - centerA).Normal;
		float cylinderHeight = (centerB - centerA).Length;

		float totalHeight = cylinderHeight + 2 * radius;

		// Build orthonormal basis
		// TODO there are better ways todo this
		Vector3 up = direction;
		Vector3 right = Vector3.Cross( direction, Vector3.Up );
		if ( right.LengthSquared < 0.001f )
		{
			right = Vector3.Cross( direction, Vector3.Right );
		}
		right = right.Normal;
		Vector3 forward = Vector3.Cross( right, up ).Normal;

		for ( int i = 0; i <= totalRings; i++ )
		{
			float v = i / (float)totalRings;
			float h = v * totalHeight;

			List<Vector3> currentRing = new();

			for ( int j = 0; j <= segments; j++ )
			{
				float u = j / (float)segments;
				float phi = u * 2.0f * MathF.PI;

				float sinPhi = MathF.Sin( phi );
				float cosPhi = MathF.Cos( phi );

				Vector3 point;

				if ( h < radius )
				{
					// Bottom hemisphere
					float t = h / radius; // t from 0 to 1
					float theta = (MathF.PI / 2) + (1 - t) * (MathF.PI / 2); // theta from π to π/2

					float sinTheta = MathF.Sin( theta );
					float cosTheta = MathF.Cos( theta );

					point = centerA
						+ (right * (sinTheta * cosPhi * radius))
						+ (forward * (sinTheta * sinPhi * radius))
						+ (up * (cosTheta * radius));
				}
				else if ( h <= radius + cylinderHeight )
				{
					// Cylinder
					float y = h - radius; // y from 0 to cylinderHeight

					point = centerA
						+ up * y
						+ (right * (cosPhi * radius))
						+ (forward * (sinPhi * radius));
				}
				else
				{
					// Top hemisphere
					float t = (h - (radius + cylinderHeight)) / radius; // t from 0 to 1
					float theta = (MathF.PI / 2) * (1 - t); // theta from π/2 to 0

					float sinTheta = MathF.Sin( theta );
					float cosTheta = MathF.Cos( theta );

					point = centerB
						+ (right * (sinTheta * cosPhi * radius))
						+ (forward * (sinTheta * sinPhi * radius))
						+ (up * (cosTheta * radius));
				}

				currentRing.Add( point );
			}

			ringPoints.Add( currentRing );
		}
	}

	private static float GetProjection( Vector3 v, Vector3 direction )
	{
		return Vector3.Dot( v, direction );
	}

	struct SubHull
	{
		public List<Vector3> Vertices;
		public Surface Surface;
	}

	struct SubMesh
	{
		public List<Vector3> Vertices;
		public List<int> Indices;
		public Surface Surface;
		public Transform PartTransform;
	}

	private List<SubHull> subHulls = new();
	private List<SubMesh> subMeshes = new();

	private List<SubHull> startCapSubHulls = new();
	private List<SubMesh> startCapSubMeshes = new();
	private List<SubHull> endCapSubHulls = new();
	private List<SubMesh> endCapSubMeshes = new();
}

// need to be less precise than default
class Vector3Comparer : IEqualityComparer<Vector3>
{
	private const float Tolerance = 0.1f;

	public bool Equals( Vector3 a, Vector3 b )
	{
		return a.AlmostEqual( b, Tolerance );
	}

	public int GetHashCode( Vector3 v )
	{
		unchecked
		{
			return v.GetHashCode();
		}
	}
}


