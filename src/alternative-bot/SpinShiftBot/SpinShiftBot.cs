using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Drawing;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

public class SpinShiftBot : Bot
{
    // Number of steps before we jump to a random point
    private int movesBeforeRandomJump = 300;

    // How far we move each step
    private double moveIncrement = 1000;

    // Counter for how many increments we've done so far
    private int moveCounter = 0;

    // RNG utility
    private Random random = new Random();

    static void Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("SpinShiftBot.json");

        var config = builder.Build();
        var botInfo = BotInfo.FromConfiguration(config);

        new SpinShiftBot(botInfo).Start();
    }

    private SpinShiftBot(BotInfo botInfo) : base(botInfo) {}

    public override void Run()
    {
        BodyColor   = Color.White;
        TurretColor = Color.Red;
        RadarColor  = Color.Black;
        ScanColor   = Color.Yellow;

        while (IsRunning)
        {
            // Continuously turn left to simulate spinning
            SetTurnLeft(999999);

            Forward(moveIncrement);
            moveCounter++;

            // Move to a random center
            if (moveCounter >= movesBeforeRandomJump)
            {
                MoveToRandomCenter();
                moveCounter = 0;
            }
        }
    }

    // Move to a random point in the arena
    private void MoveToRandomCenter()
    {
        // Pick a random location within the arena
        // We go up to 80% of the arena to avoid hitting walls
        double margin = 0.8;
        double randX = random.NextDouble() * ArenaWidth * margin;
        double randY = random.NextDouble() * ArenaHeight * margin;

        // Move towards that point
        double bearing = BearingTo(randX, randY);
        if (bearing >= 0)
        {
            TurnRight(bearing);
        }
        else
        {
            TurnLeft(-bearing);
        }

        double distance = DistanceTo(randX, randY);
        Forward(distance);

        // Small adjustment
        TurnLeft(30);
    }

    // Fire a bullet upon scanning an enemy
    public override void OnScannedBot(ScannedBotEvent evt)
    {
        Fire(3);
    }

    // Handle collisions
    public override void OnHitBot(HitBotEvent e)
    {
        double bearing = BearingTo(e.X, e.Y);
        if (bearing > -10 && bearing < 10)
        {
            Fire(3);
        }
        if (e.IsRammed)
        {
            TurnLeft(30);
        }
    }

    // If the bot hit a wall, move towards the center of the arena for more space
    public override void OnHitWall(HitWallEvent e)
    {
        double centerX = ArenaWidth / 2;
        double centerY = ArenaHeight / 2;

        double bearing = BearingTo(centerX, centerY);
        if (bearing >= 0)
        {
            TurnRight(bearing);
        }
        else
        {
            TurnLeft(-bearing);
        }
        
        double distance = DistanceTo(centerX, centerY);
        Forward(distance);
    }
}
