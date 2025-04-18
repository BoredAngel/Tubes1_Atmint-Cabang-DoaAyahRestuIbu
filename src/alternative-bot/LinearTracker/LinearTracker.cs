using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;

public class LinearTracker : Bot
{   
    int l_board = 100;
    int r_board = 700;
    int u_board = 100;
    int d_board = 500;
    int minimal_turn_to_reverse = 10;
    double target_X, target_Y;
    int counter_shot = 0;
    int counter_hit = 0;

    bool clockwise = true;
    int hit_bot_turn_recovery = 0;
    /* A bot that drives forward and backward, and fires a bullet */
    static void Main(string[] args)
    {
        new LinearTracker().Start();
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

    /// <summary>
    /// Calculates a bullet power (0.5 to 5.0) and firing angle (in radians)
    /// for a predictive shot based on linear targeting.
    /// Returns [bulletPower, firingAngle].
    /// </summary>
    /// <param name="myX">Your robot's X position</param>
    /// <param name="myY">Your robot's Y position</param>
    /// <param name="enemyX">Enemy's X position</param>
    /// <param name="enemyY">Enemy's Y position</param>
    /// <param name="enemyHeading">Enemy's heading (radians)</param>
    /// <param name="enemyVelocity">Enemy's speed (pixels per tick)</param>
    /// <returns>A double[] of length 2: [bulletPower, firingAngle]</returns>
    public static double[] GetPredictedShot(
        double myX, double myY,
        double enemyX, double enemyY,
        double enemyHeading, double enemyVelocity
    )
    {
        enemyHeading = enemyHeading * Math.PI / 180;
        double dx = enemyX - myX;
        double dy = enemyY - myY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        double bulletPower;
        if (distance < 75)
        {
            bulletPower = 4.0;
        }
        else if (distance < 100)
        {
            bulletPower = 3.0;
        }
        else if (distance < 125)
        {
            bulletPower = 2.0;
        }
        else if (distance < 175)
        {
            bulletPower = 1.0;
        }
        else
        {
            bulletPower = 0.5;
        }

        double bulletSpeed = 20.0 - 3.0 * bulletPower;

        double vx = enemyVelocity * Math.Cos(enemyHeading);
        double vy = enemyVelocity * Math.Sin(enemyHeading);

        double a = vx * vx + vy * vy - bulletSpeed * bulletSpeed;
        double b = 2.0 * (dx * vx + dy * vy);
        double c = dx * dx + dy * dy;

        double discriminant = b * b - 4.0 * a * c;
        double t = 0.0;

        if (Math.Abs(a) > 1e-9 && discriminant >= 0.0) {
            double sqrtDisc = Math.Sqrt(discriminant);
            double t1 = (-b + sqrtDisc) / (2.0 * a);
            double t2 = (-b - sqrtDisc) / (2.0 * a);

            if (t1 > 0.0 && t2 > 0.0) {
                t = Math.Min(t1, t2);
            }
            else if (t1 > 0.0) {
                t = t1;
            }
            else if (t2 > 0.0) {
                t = t2;
            }
            else {
                t = 0.0;
            }
        }
        else {
            // No valid solution => fallback to direct aim
            t = 0.0;
        }

        double predictedX = enemyX + vx * t;
        double predictedY = enemyY + vy * t;

        // Console.WriteLine("Enemy XY : " + enemyX + " " + enemyY);
        // Console.WriteLine("Predicted XY : " + predictedX + " " + predictedY);
        // Console.WriteLine("Velocity XY : " + vx + " " + vy);

        double firingAngle = Math.Atan2(predictedY - myY, predictedX - myX);
        
        firingAngle = firingAngle * 180 / Math.PI;

        //Console.WriteLine("Predicted shot: bulletPower={0}, firingAngle={1}", bulletPower, firingAngle);

        return new double[] { bulletPower, firingAngle };
    }


    LinearTracker() : base(BotInfo.FromFile("LinearTracker.json")) { }

    public override void Run()
    {
        /* Customize bot colors, read the documentation for more information */
        BodyColor     = Color.FromArgb(197, 133, 48); 
        TurretColor   = Color.FromArgb(205, 205, 205);
        RadarColor    = Color.FromArgb(255, 0, 127);  
        BulletColor   = Color.FromArgb(255, 0, 255);  
        ScanColor     = Color.FromArgb(255, 0, 127);  
        TracksColor   = Color.FromArgb(155, 155, 155); 
        GunColor      = Color.FromArgb(175, 175, 175);


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

        // ---------- TANK MOVEMENT ---------- //
        bool turned = false;
        if (minimal_turn_to_reverse < 0 && position_on_edge()) {
            clockwise = !clockwise;
            minimal_turn_to_reverse = 10;
            turned = true;
        }
        
        if (clockwise) {
            SetTurnLeft(direction_bearing + getOffset1(distance));
            SetForward(10000);
        }
        else {
            SetTurnLeft(direction_bearing + getOffset2(distance));
            SetForward(-10000);
        }

        
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

        // -------- HANDLE GUN -------- //
        double[] shot = GetPredictedShot(X, Y, e.X, e.Y, e.Direction, e.Speed);
        double bulletPower = shot[0];
        double firingAngle = shot[1];

        double gunTurn = NormalizeRelativeAngle(firingAngle - GunDirection);
        SetTurnGunLeft(gunTurn);

        if (gunTurn < 2.5) {
            Fire(bulletPower);
        }
    }

    public override void  OnBulletHit(BulletHitBotEvent bulletHitBotEvent) {
        counter_hit += 1;
        counter_shot += 1;

        // Console.Write("Bullet Shot : " + counter_shot + "\n" + "Bullet Hit : " + counter_hit + " ");
        // Console.Write("Hit Rate : " + (double)counter_hit / counter_shot + "\n");
    }

    public override void OnBulletHitWall(BulletHitWallEvent bulletHitWallEvent) {
        counter_shot += 1;
        // Console.Write("Bullet Shot : " + counter_shot + "\n" + "Bullet Hit : " + counter_hit + " ");
        // Console.Write("Hit Rate : " + (double)counter_hit / counter_shot + "\n");
    }

    public override void OnBulletHitBullet(BulletHitBulletEvent bulletHitBulletEvent) {
        counter_shot += 1;
        // Console.Write("Bullet Shot : " + counter_shot + "\n" + "Bullet Hit : " + counter_hit + " ");
        // Console.Write("Hit Rate : " + (double)counter_hit / counter_shot + "\n");
    }

    public override void OnHitBot(HitBotEvent e) {
        if (minimal_turn_to_reverse < 5) {
            clockwise = !clockwise;
            minimal_turn_to_reverse = 10;
        }
    }
}
