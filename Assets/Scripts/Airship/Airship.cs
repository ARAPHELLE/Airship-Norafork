using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Airship : MonoBehaviour
{
    // Singleton stuff
    public static Airship instance;
    private void Awake()
    {
        instance = this;
        // Enable game hud
        HUD.SetBlack(false);
        //HUD.SetFuelVisibility(true);
    }

    public Rigidbody rb; // Airship rb (not sure if used)
    public CharacterController playerController; // To move the player with the ship
    public List<Transform> kiddos; // Objects that the ship should move (crates, rat etc)
    public float kidRemoveRange = 40f; // Don't drag along objects further away than this
    // TODO: Add objects to kiddos list when they come within range
    // (currently, once a barrel etc is removed from the list,
    //      they are never a child of the ship again)
    public Wheel wheel; // Wheel yknow
    public Vector3 movement = new Vector3(0, 0, 5f); // Speed of the ship
    private Vector3 moveDelta;
    private float vertMove;
    public float turnSpeed = 0.2f; // Turn speed
    public float turnAmount = 20f; // Turn angle

   
    [Space]
    [Min(0f)]
    public float fuelBurnRate = 1f; // How many fuel units are burnt, per second
    [Range(0f, 1f)]
    public float startingFuel = 0.5f; // Percentage (0-1) of how full a tank to start with
    public float maxFuel = 100f; // The max fuel the ship holds

    [ReadOnly] public float startingSecondsOfFuel; // Inspector values, showing the equivalant
    [ReadOnly] public float maxSecondsOfFuel;       // seconds of fuel, from fuel units

    [Space]
    public GrappleHook leftHook; // Grapple hooks
    public GrappleHook rightHook;
    public float hookGrabbingTurnAmount = 2f;
    // When reeling in a cache, the amount the ship should turn towards that direction

    [Space]
    [Rename("Pickup Spawn Position")]
    public Transform spawnCrapHere;
    // Place on the ship to spawn cargo (temporary)

    // Static float for the ships current turn amount
    public static float Turn { get; private set; }
    float turnPlusMinus1;
    // Actual value, for smoothing formula

    static float fuel = 50f;
    public static float Fuel
    {
        get => fuel;
        set => fuel = Mathf.Clamp(value, 0, instance.maxFuel);
    }
    public static float Fuel01 => Remap.Float01(Fuel, 0, instance.maxFuel);
    // Fuel idk

    bool crashed; // Self explanatory bruh
    // (also temporary, ship damage etc will be later)

    private void Start()
    {
        IntroSpiel(); // Tutorial text
        fuel = maxFuel * startingFuel; // Starting fuel
    }

    void Update()
    {
        UpdateFuel();

        float desiredTurn = 0f;
        desiredTurn += leftHook.GetTurnAmount() * hookGrabbingTurnAmount;
        desiredTurn += rightHook.GetTurnAmount() * hookGrabbingTurnAmount;
        // Turn the ship towards 

        if (wheel.IsInteracting)
        {
            desiredTurn += PlayerInputs.Movement.x;
            vertMove = PlayerInputs.Movement.y;
        }
            
        // Turn the ship if the player is interacting with the wheel

        // Easing the turn value so steering is smoothed
        turnPlusMinus1 = Mathf.MoveTowards(turnPlusMinus1, desiredTurn, Time.deltaTime * turnSpeed);
        Turn = (EaseInOutQuad(0, 1, (turnPlusMinus1 + 1) / 2f) * 2 - 1) * turnAmount;

        // VVV How much the ship will move
        moveDelta = (-transform.forward * movement.z + Vector3.up * (vertMove * 5.0f)) * Time.deltaTime;

        if (DockingSystem.Docking)
        {
            //delta = Vector3.zero;
            moveDelta = transform.position.DirectionTo_NoNormalize
                (DockingSystem.ActiveSystem.transform.position) * Time.deltaTime;
            //desiredTurn = 0;
            turnPlusMinus1 = 0;
            float y = DockingSystem.ActiveSystem.transform.eulerAngles.y;
            Turn = y - transform.eulerAngles.y;
            Turn /= 2;
           
            if (transform.eulerAngles.y < 2)
            {
                DockingSystem.Docked = true;
                HUD.SetDepartureIndicator(true);

            }
        }

        
        // ^^^ Move the player and children along with the ship

        // VVV Move and rotate the ship itself
        transform.position += moveDelta;
        transform.Rotate(Vector3.up * Turn * Time.deltaTime);



        //transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime, Space.Self);
        //rb.velocity = -transform.forward * moveSpeed;
        //rb.MovePosition(transform.position + delta);

        //MovePlayer(transform.position - pos, turn);
    }

    private void LateUpdate()
    {
        MovePlayer(moveDelta, Turn);
        MoveKids(moveDelta, Turn);
    }

    void IntroSpiel()
    {
        // Using a timer util class to show the tutorial text
        Timer.New(0.0f, () => PopUp.Show("Welcome to Airship Game!"));
        Timer.New(4.0f, () => PopUp.Show("Right Click/E/F to interact with the wheel/grapple hooks (+- for sens)", 4.0f));
        Timer.New(8.0f, () => PopUp.Show("Shoot fuel caches with LMB", 3.0f));
        Timer.New(11.0f, () => PopUp.Show("Reach the end sphere, follow the arrows!", 4.0f));
        Timer.New(16.0f, () => PopUp.Show("Good Luck!", 3.0f));
    }

    public static void Crash(string reason, float time)
    {
        if (!instance.crashed)
        {
            Timer.DestroyAll(-1);
            instance.crashed = true;
            PopUp.Show(reason, time);
            HUD.SetBlack(true);
            Timer.Create(time, SceneManager.ReloadCurrentLevel);
            // Sets screen to black, shows text, reloads level after time
        }
    }

    void UpdateFuel()
    {
        Fuel -= Time.deltaTime * fuelBurnRate;
        if (Fuel <= 0)
        {
            Crash("Ran out of fuel! Collect floating caches!", 5f);
        }
        //HUD.SetFuel(Fuel);
        // Decreases fuel and sets the fuel bar
    }

    void MovePlayer(Vector3 delta, float y)
    {
        //playerController.Move(delta);
        playerController.enabled = false;
        playerController.transform.position += delta;
        playerController.transform.RotateAround(transform.position, Vector3.up, y * Time.deltaTime);
        playerController.enabled = true;
        // Moves and rotates player, must disable cc to rotate player
    }

    void MoveKids(Vector3 delta, float y)
    {
        // Removes kids that are far away
        for (int i = kiddos.Count; i > 0;)
        {
            i--;
            if (kiddos[i] == null || kiddos[i].position
                .SqrDistance(transform.position) > kidRemoveRange * kidRemoveRange)
                kiddos.RemoveAt(i);
        }

        // Moves kids
        foreach (Transform child in kiddos)
        {
            child.position += delta;
            child.transform.RotateAround(transform.position, Vector3.up, y * Time.deltaTime);
        }
    }

    // https://gitlab.com/gamedev-public/unity/-/blob/main/Scripts/Extensions/Easings/EasingUtility.cs
    // Ease function
    public static float EaseInOutQuad(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return end * 0.5f * value * value + start;
        value--;
        return -end * 0.5f * (value * (value - 2) - 1) + start;
    }

    // Just visual for kid removal range
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, kidRemoveRange);
    }


    // Inspector values
    private void OnValidate()
    {
        startingSecondsOfFuel = maxFuel * startingFuel / fuelBurnRate;
        maxSecondsOfFuel = maxFuel / fuelBurnRate;
    }
}
