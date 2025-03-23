using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Collections.Generic;


public class adaptive_tracker : Bot
{   
    int l_board = 100;
    int r_board = 700;
    int u_board = 100;
    int d_board = 500;
    int minimal_turn_to_reverse = 10;
    double this_hp_bef = 100;
    double enemy_hp_bef = 100;
    double enemy_id_bef = -1;
    double target_X, target_Y;
    List<List<ScanData>> scan_data;
    

    class ScanData {
        public double X;
        public double Y;
        public double Direction;
        public double Speed;
        public double Energy;
        public int ID;
        public int Turn;
    }

    public void UpdateData(double X, double Y, double Direction, double Speed, double Energy, int ID) {
        scan_data[ID].Add(new ScanData {X = X, Y = Y, Direction = Direction, Speed = Speed, Energy = Energy, ID = ID, Turn = TurnNumber});
        if (scan_data[ID].Count > 10) {
            scan_data[ID].RemoveAt(0);
        }

        while (scan_data[ID][0].Turn < TurnNumber - 10) {
            scan_data[ID].RemoveAt(0);
        }

        //Console.WriteLine("ID : " + ID);
    }

    public bool IsTurning(int ID) {
        int len = scan_data[ID].Count;
        
        if (len < 2) {
            return false;
        }

        if (scan_data[ID][len - 1].Direction - scan_data[ID][len - 2].Direction > 0) {
            return true;
        }

        return false;
    }

    double GetTurnRateDegreesPerTick(int ID)
    {
        // If not enough data, assume no turning
        if (scan_data[ID].Count < 2)
            return 0.0;

        int len = scan_data[ID].Count;
        double latestDir = scan_data[ID][len - 1].Direction;    // newest heading
        double prevDir   = scan_data[ID][len - 2].Direction;    // previous heading
        double delta     = latestDir - prevDir;                 // heading change (deg)
        double turnRate  = delta;                               // deg per 1 tick (since scans are 1 turn apart)

        // Optional: normalize to [-180, +180] or something similar
        // E.g., if an enemy goes from 359° to 1°, naive delta = +2°, but from 1° to 359°, naive delta = -2°
        if (turnRate > 180)
            turnRate -= 360;
        else if (turnRate < -180)
            turnRate += 360;

        return turnRate; // in degrees/turn
    }

    /// <summary>
    /// Predicts where a *turning* enemy will be, using a small time-step iteration.
    /// Returns [bulletPower, firingAngleDegrees].
    /// </summary>
    public static double[] GetTurningPredictiveShot(
        double myX, double myY,
        double enemyX, double enemyY,
        double enemyHeadingDeg,  // current heading in degrees
        double enemySpeed,       // current speed (px/turn)
        double turnRateDeg,      // estimated turning rate (deg/turn)
        double desiredBulletPower // you can choose or compute this externally
    )
    {
        // 1) Convert everything to radians for math
        double enemyHeading = enemyHeadingDeg * Math.PI / 180.0;
        double turnRate     = turnRateDeg      * Math.PI / 180.0; // rad/turn

        // 2) Compute bullet speed from your bullet power (standard Robocode formula)
        //    bulletSpeed = 20 - 3 * power
        //    or adapt if your game rules differ
        double bulletSpeed = 20.0 - 3.0 * desiredBulletPower;

        // 3) We'll search for a time 't' (in turns) between 0 and some max
        //    that best satisfies "distance to predicted position = bulletSpeed * t".
        //    We'll iterate in small steps (e.g., 0.1 turn).
        double bestT = 0.0;
        double minDiff = double.MaxValue;

        double maxTime = 50;     // 50 turns is arbitrary; adjust as needed
        double step    = 0.5;    // bigger steps = faster but less accurate

        for (double t = 0.0; t < maxTime; t += step)
        {
            // predict enemy heading after t turns
            double headingAtT = enemyHeading + turnRate * t;

            // predict position after t turns, assuming constant speed + turning
            double predX = enemyX + enemySpeed * t * Math.Cos(enemyHeading);
            double predY = enemyY + enemySpeed * t * Math.Sin(enemyHeading);

            // But wait, the enemy heading is changing over time, so we can do a
            // more fine-grained sub-iteration. For demonstration, let's do a
            // simpler approach: assume heading is constant over each small 'step.'
            // A more accurate method would break the total time t into smaller
            // increments and update heading incrementally.

            // distance from my bot to predicted position
            double dist = Distance(myX, myY, predX, predY);

            // bullet travel distance if it flies for 't' turns:
            double bulletDist = bulletSpeed * t;

            // how close are we to an intercept?
            double diff = Math.Abs(dist - bulletDist);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestT   = t;
            }
        }

        // 4) Now we have bestT. Let’s compute the final predicted position more carefully
        //    with a smaller step iteration. This is optional, or you can simply re-run the
        //    same logic for bestT. We'll do a mini incremental simulation for better accuracy:
        double finalX = enemyX;
        double finalY = enemyY;
        double finalHeading = enemyHeading;

        double dt = 0.1; // sub-step
        for (double elapsed = 0; elapsed < bestT; elapsed += dt)
        {
            finalX += enemySpeed * dt * Math.Cos(finalHeading);
            finalY += enemySpeed * dt * Math.Sin(finalHeading);
            finalHeading += turnRate * dt;
        }

        // 5) Compute the firing angle from my bot to predicted position
        double angleRadians = Math.Atan2(finalY - myY, finalX - myX);
        double firingAngleDeg = angleRadians * 180.0 / Math.PI;

        return new double[] { desiredBulletPower, firingAngleDeg };
    }

    // Utility distance function
    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx*dx + dy*dy);
    }


    bool clockwise = true;
    int hit_bot_turn_recovery = 0;
    /* A bot that drives forward and backward, and fires a bullet */
    static void Main(string[] args)
    {
        new adaptive_tracker().Start();
    }

    public double getOffset1(double distance) {
        if (distance >= 120) {
            double offset = 90 - (distance - 120) * 0.2;
            return Math.Max(offset, 60);
        } else if (distance <= 100) {
            double offset = 90 + (100 - distance) * 0.8;
            return Math.Min(offset, 120);
        } else {
            return 90 + Math.Sin(TurnNumber * Math.PI / 180) * 90;
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
            return 90 + Math.Sin(TurnNumber * Math.PI / 180) * 90;
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
        // 1) Compute the distance to the enemy
        enemyHeading = enemyHeading * Math.PI / 180;
        double dx = enemyX - myX;
        double dy = enemyY - myY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // 2) Choose a bullet power (higher power if enemy is closer)
        //    (This is a custom range up to 5.0, not standard Robocode.)
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

        // 3) Compute bullet speed (adjust formula to match your game rules)
        //    For standard Robocode: bulletSpeed = 20 - 3 * bulletPower
        double bulletSpeed = 20.0 - 3.0 * bulletPower;

        // 4) Decompose enemy velocity into X, Y components
        double vx = enemyVelocity * Math.Cos(enemyHeading);
        double vy = enemyVelocity * Math.Sin(enemyHeading);

        // 5) Solve the quadratic for interception time t:
        //      (dx + vx*t)^2 + (dy + vy*t)^2 = (bulletSpeed * t)^2
        //    => a = vx^2 + vy^2 - bulletSpeed^2
        //       b = 2 * (dx*vx + dy*vy)
        //       c = dx^2 + dy^2
        double a = vx * vx + vy * vy - bulletSpeed * bulletSpeed;
        double b = 2.0 * (dx * vx + dy * vy);
        double c = dx * dx + dy * dy;

        double discriminant = b * b - 4.0 * a * c;
        double t = 0.0;

        // If the quadratic has a real solution
        if (Math.Abs(a) > 1e-9 && discriminant >= 0.0)
        {
            double sqrtDisc = Math.Sqrt(discriminant);
            double t1 = (-b + sqrtDisc) / (2.0 * a);
            double t2 = (-b - sqrtDisc) / (2.0 * a);

            // Choose the smallest positive time
            if (t1 > 0.0 && t2 > 0.0)
            {
                t = Math.Min(t1, t2);
            }
            else if (t1 > 0.0)
            {
                t = t1;
            }
            else if (t2 > 0.0)
            {
                t = t2;
            }
            else
            {
                // No positive time => fallback to direct aim
                t = 0.0;
            }
        }
        else
        {
            // No valid solution => fallback to direct aim
            t = 0.0;
        }

        // 6) Predict enemy position at time t
        double predictedX = enemyX + vx * t;
        double predictedY = enemyY + vy * t;

        // Console.WriteLine("Enemy XY : " + enemyX + " " + enemyY);
        // Console.WriteLine("Predicted XY : " + predictedX + " " + predictedY);
        // Console.WriteLine("Velocity XY : " + vx + " " + vy);

        // 7) Compute the firing angle in radians
        double firingAngle = Math.Atan2(predictedY - myY, predictedX - myX);
        
        // 8) Turn angle into a relative angle
        firingAngle = firingAngle * 180 / Math.PI;

        //Console.WriteLine("Predicted shot: bulletPower={0}, firingAngle={1}", bulletPower, firingAngle);

        return new double[] { bulletPower, firingAngle };
    }


    adaptive_tracker() : base(BotInfo.FromFile("adaptive_tracker.json")) { }

    public override void Run()
    {
        /* Customize bot colors, read the documentation for more information */
        BodyColor     = Color.FromArgb(58, 122, 207); 
        TurretColor   = Color.FromArgb(50, 50, 50);  
        RadarColor    = Color.FromArgb(0, 255, 128);
        BulletColor   = Color.FromArgb(0, 255, 0);   
        ScanColor     = Color.FromArgb(0, 255, 128); 
        TracksColor   = Color.FromArgb(100, 100, 100);
        GunColor      = Color.FromArgb(80, 80, 80);  


        scan_data = new List<List<ScanData>>();
        scan_data.Add(new List<ScanData>());
        scan_data.Add(new List<ScanData>());
        scan_data.Add(new List<ScanData>());
        scan_data.Add(new List<ScanData>());


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
        UpdateData(e.X, e.Y, e.Direction, e.Speed, e.Energy, e.ScannedBotId);

        double[] shot;
        if (!IsTurning(e.ScannedBotId)) shot = GetPredictedShot(X, Y, e.X, e.Y, e.Direction, e.Speed);
        else {
            if (distance < 200) {
            shot = GetTurningPredictiveShot(X, Y, e.X, e.Y, e.Direction, e.Speed, GetTurnRateDegreesPerTick(e.ScannedBotId), 2.0);
        }
        else if (distance < 400) {
            shot = GetTurningPredictiveShot(X, Y, e.X, e.Y, e.Direction, e.Speed, GetTurnRateDegreesPerTick(e.ScannedBotId), 1.0);
        }
        else {
            shot = GetTurningPredictiveShot(X, Y, e.X, e.Y, e.Direction, e.Speed, GetTurnRateDegreesPerTick(e.ScannedBotId), 0.5);
        }
        }
        double bulletPower = shot[0];
        double firingAngle = shot[1];

        double gunTurn = NormalizeRelativeAngle(firingAngle - GunDirection);
        SetTurnGunLeft(gunTurn);

        // -------- FIRE -------- //
        SetFire(bulletPower);


        enemy_hp_bef = e.Energy;
        this_hp_bef = Energy;
        enemy_id_bef = e.ScannedBotId;
    }

    public override void OnHitBot(HitBotEvent e) {
        if (minimal_turn_to_reverse < 5) {
            clockwise = !clockwise;
            minimal_turn_to_reverse = 10;
        }
    }
}
