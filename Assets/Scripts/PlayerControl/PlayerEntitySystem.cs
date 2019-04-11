using UnityEngine;
using UnityEditor;

using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

using Unity.Rendering;

public class PlayerEntitySystem : ComponentSystem
{
    EntityManager entityManager;
    CellSystem cellSystem;
    int squareWidth;

    Camera camera;
    float cameraSwivelSpeed = 1;
    float3 currentOffset = new float3(0, 10, -10);

    public GameObject player;

    const float playerSpeed = 20f;

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        cellSystem = World.Active.GetOrCreateSystem<CellSystem>();
        squareWidth = TerrainSettings.mapSquareWidth;
    }

    protected override void OnStartRunning()
    {
        camera = GameObject.FindObjectOfType<Camera>();
        player = GameObject.FindGameObjectWithTag("Player");
    }

    protected override void OnUpdate()
    {
        DebugSystem.Text("player position", player.transform.position.ToString());

        MoveCamera();

        MovePlayer();

        ClampToTerrainHeight();
    }

    void MovePlayer()
    {
        float3 playerPosition = player.transform.position;

        //  Camera forward ignoring x axis tilt
        float3 forward  = math.normalize(playerPosition - new float3(camera.transform.position.x, playerPosition.y, camera.transform.position.z));

         //  Move relative to camera angle
        float3 x = UnityEngine.Input.GetAxis("Horizontal")  * -(float3)camera.transform.right;
        float3 z = UnityEngine.Input.GetAxis("Vertical")    * -(float3)forward;

        //  Update movement component
        float3 move = (x + z) * playerSpeed;
        player.transform.Translate(new float3(move.x, 0, move.z) * Time.deltaTime);
    }

    void ClampToTerrainHeight()
    {   
        float3 playerPosition = player.transform.position;

        float height = cellSystem.GetHeightAtPosition(playerPosition);

        float3 newPosition = new float3(playerPosition.x, height, playerPosition.z);

        player.transform.position = newPosition;
    }

    void MoveCamera()
    {
        float3 playerPosition = player.transform.position;

        float3 oldPosition      = camera.transform.position;
        Quaternion oldRotation  = camera.transform.rotation;

        //  Rotate around player y axis
        bool Q = Input.GetKey(KeyCode.Q);
        bool E = Input.GetKey(KeyCode.E);
        Quaternion cameraSwivel = Quaternion.identity;
        if( !(Q && E) )
        {
            if (Q) cameraSwivel     = Quaternion.Euler(new float3(0, -cameraSwivelSpeed, 0));
            else if(E) cameraSwivel = Quaternion.Euler(new float3(0, cameraSwivelSpeed, 0));
        }
        float3 rotateOffset = (float3)oldPosition - RotateAroundCenter(cameraSwivel, oldPosition, playerPosition);

        //  Zoom with mouse wheel
        float3 zoomOffset = Input.GetAxis("Mouse ScrollWheel") * -currentOffset;

        //  Apply position changes
        currentOffset += rotateOffset;

        //  Clamp zoom 
        float3 withZoom = currentOffset + zoomOffset;
        float magnitude = math.sqrt(math.pow(withZoom.x, 2) + math.pow(withZoom.y, 2) + math.pow(withZoom.z, 2));
        if(magnitude > 10 && magnitude < 150)
        {
            //  x & z smoothed for zoom because y is smoothed for everything later
            //  This prevents jumpy camera movement when zooming
            currentOffset = new float3(
                math.lerp(currentOffset.x, withZoom.x, 0.1f),
                withZoom.y,
                math.lerp(currentOffset.z, withZoom.z, 0.1f)
            );
        }

        //  New position and rotation without any smoothing
        float3 newPosition      = playerPosition + currentOffset;
        Quaternion newRotation  = Quaternion.LookRotation(playerPosition - (float3)oldPosition, Vector3.up);

        //  Smooth y for everything
        //  Movement depends on camera angle and lerping x & z causes camera
        //  to turn when moving, so we only smooth y for movement
        float yLerp = math.lerp(oldPosition.y, newPosition.y, 0.1f);

        //  Apply new position and rotation softly
        camera.transform.position = new float3(newPosition.x, yLerp, newPosition.z);
        camera.transform.rotation = Quaternion.Lerp(oldRotation, newRotation, 0.1f);
    }

    public static float3 RotateAroundCenter(Quaternion rotation, Vector3 position, Vector3 centre)
    {
        return rotation * (position - centre) + centre;
    }
}