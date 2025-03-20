using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;

public class temp_bot : Bot
{   
    int l_board = 100;
    int r_board = 700;
    int u_board = 100;
    int d_board = 500;

    bool clockwise = true;
    int minimal_turn_to_reverse = 19;
    /* A bot that drives forward and backward, and fires a bullet */
    static void Main(string[] args)
    {
        new temp_bot().Start();
    }

    public double getOffset1(double distance) {
        if (distance >= 120) {
            double offset = 90 - (distance - 120) * 0.2;
            return Math.Max(offset, 60);
        } else if (distance <= 100) {
            double offset = 90 + (100 - distance) * 0.8;
            return Math.Min(offset, 120);
        } else {
            return 90;
        }
    }

    public double getOffset2(double distance) {
        if (distance <= 100) {
            double offset = 90 - (100 - distance) * 0.8;
            return Math.Max(offset, 60);
        } else if (distance >= 120) {
            double offset = 90 + (distance - 120) * 0.3;
            return Math.Min(offset, 120);
        } else {
            return 90;
        }
    }


    public bool position_on_edge() {
        return (X <= l_board || X >= r_board || Y <= u_board || Y >= d_board);
    }


    temp_bot() : base(BotInfo.FromFile("temp_bot.json")) { }

    public override void Run()
    {
        /* Customize bot colors, read the documentation for more information */
        BodyColor = Color.FromArgb(0x00, 0x00, 0x00);
        TurretColor = Color.FromArgb(0x00, 0x00, 0x00);
        RadarColor = Color.FromArgb(0x00, 0x00, 0x00);
        BulletColor = Color.FromArgb(0x00, 0x00, 0x00);
        ScanColor = Color.FromArgb(0x00, 0x00, 0x00);
        TracksColor = Color.FromArgb(0x00, 0x00, 0x00);
        GunColor = Color.FromArgb(0x00, 0x00, 0x00);


        while (IsRunning)
        {
            TurnRadarRight(1000);
            minimal_turn_to_reverse--;
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        double enemy_bearing = DirectionTo(e.X, e.Y);
        double direction_bearing = BearingTo(e.X, e.Y);
        double gun_bearing = GunBearingTo(e.X, e.Y);
        double radar_bearing = RadarBearingTo(e.X, e.Y);
        double correction = 0;
        double distance = DistanceTo(e.X, e.Y);

        // Console.WriteLine("Radar Direction : " + RadarDirection);
        // Console.WriteLine("Enemy Direction : " + enemy_bearing);
        // Console.WriteLine(radar_bearing + " " + RadarTurnRate);
        // ---------- RADAR MOVEMENT ---------- 

        if (radar_bearing <= 0) {
            correction = 5;
            SetTurnRadarLeft(radar_bearing - correction);
        }
        else {
            correction = 5;
            SetTurnRadarLeft(radar_bearing + correction);
        }
        SetTurnGunLeft(gun_bearing);



        // ---------- TANK MOVEMENT ---------- //
        if (minimal_turn_to_reverse < 0 && position_on_edge()) {
            clockwise = !clockwise;
            minimal_turn_to_reverse = 10;
        }

        if (clockwise) {
            SetTurnLeft(direction_bearing + getOffset1(distance));
            SetForward(10000);
        }
        else {
            SetTurnLeft(direction_bearing + getOffset2(distance));
            SetForward(-10000);
        }


        // -------- HANDLE GUN -------- //
        if (distance <= 10) Fire(Math.Min(Energy, 30));
        else if (distance <= 30) Fire(Math.Min(Energy, 20));
        else if (distance <= 50) Fire(Math.Min(Energy, 15));
        else if (distance <= 80) Fire(Math.Min(Energy, 10));
        else if (distance <= 120) Fire(Math.Min(Energy, 5));
        else if (distance <= 160) Fire(Math.Min(Energy, 2));
        else {
            Go();
        }
    }
}
