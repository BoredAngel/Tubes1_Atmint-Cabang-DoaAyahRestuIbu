using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Collections.Generic;


public class AdaptiveTracker : Bot
{   
    int l_board = 100;
    int r_board = 700;
    int u_board = 100;
    int d_board = 500;
    int minimal_turn_to_reverse = 10;
    double target_X, target_Y;
    List<List<ScanData>> scan_data;
    int counter_shot = 0;
    int counter_hit = 0;
    

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
        if (scan_data[ID].Count < 2)
            return 0.0;

        int len = scan_data[ID].Count;
        double latestDir = scan_data[ID][len - 1].Direction;  
        double prevDir   = scan_data[ID][len - 2].Direction;  
        double delta     = latestDir - prevDir;                 
        double turnRate  = delta;                              

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
        double enemyHeadingDeg,
        double enemySpeed,    
        double turnRateDeg,  
        double desiredBulletPower 
    )
    {
        double enemyHeading = enemyHeadingDeg * Math.PI / 180.0;
        double turnRate = turnRateDeg * Math.PI / 180.0; 
        
        double bulletSpeed = 20.0 - 3.0 * desiredBulletPower;
        
        double bestT = 0.0;
        double minDiff = double.MaxValue;
        double maxTime = 50; 
        double step = 0.5;      

        for (double t = 0.0; t < maxTime; t += step)
        {
            double[] prediction = PredictPositionWithArc(
                enemyX, enemyY, enemyHeading, enemySpeed, turnRate, t);
            
            double predX = prediction[0];
            double predY = prediction[1];
            
            double dist = Distance(myX, myY, predX, predY);
            double bulletDist = bulletSpeed * t;
            
            double diff = Math.Abs(dist - bulletDist);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestT = t;
            }
        }
        
        double refinedMinDiff = minDiff;
        double refinedBestT = bestT;
        double refinedStep = step / 10.0;
        double refinedStart = Math.Max(0, bestT - step);
        double refinedEnd = bestT + step;
        
        for (double t = refinedStart; t < refinedEnd; t += refinedStep)
        {
            double[] prediction = PredictPositionWithArc(
                enemyX, enemyY, enemyHeading, enemySpeed, turnRate, t);
            
            double predX = prediction[0];
            double predY = prediction[1];

            double dist = Distance(myX, myY, predX, predY);
            double bulletDist = bulletSpeed * t;
            
            double diff = Math.Abs(dist - bulletDist);
            if (diff < refinedMinDiff)
            {
                refinedMinDiff = diff;
                refinedBestT = t;
            }
        }
        
        double[] finalPrediction = PredictPositionWithArc(
            enemyX, enemyY, enemyHeading, enemySpeed, turnRate, refinedBestT);
        
        double angleRadians = Math.Atan2(finalPrediction[1] - myY, finalPrediction[0] - myX);
        double firingAngleDeg = angleRadians * 180.0 / Math.PI;
        
        return new double[] { desiredBulletPower, firingAngleDeg };
    }

    private static double[] PredictPositionWithArc(
        double x, double y, double heading, double speed, double turnRate, double time)
    {
        if (Math.Abs(turnRate) < 0.000001)
        {
            return new double[] { 
                x + speed * time * Math.Cos(heading),
                y + speed * time * Math.Sin(heading),
                heading
            };
        }
        
        double radius = speed / turnRate;
        
        double centerX = x - radius * Math.Sin(heading);
        double centerY = y + radius * Math.Cos(heading);
        
        double newHeading = heading + turnRate * time;
        
        double newX = centerX + radius * Math.Sin(newHeading);
        double newY = centerY - radius * Math.Cos(newHeading);
        
        return new double[] { newX, newY, newHeading };
    }


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
        new AdaptiveTracker().Start();
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
        enemyHeading = enemyHeading * Math.PI / 180;
        double dx = enemyX - myX;
        double dy = enemyY - myY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        double bulletPower;
        if (distance < 75) {
            bulletPower = 4.0;
        }
        else if (distance < 100) {
            bulletPower = 3.0;
        }
        else if (distance < 125) {
            bulletPower = 2.0;
        }
        else if (distance < 175) {
            bulletPower = 1.0;
        }
        else {
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

        if (Math.Abs(a) > 1e-9 && discriminant >= 0.0)
        {
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
            t = 0.0;
        }

        // 6) Predict enemy position at time t
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


    AdaptiveTracker() : base(BotInfo.FromFile("AdaptiveTracker.json")) { }

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
            if (distance < 75) {
                shot = GetTurningPredictiveShot(X, Y, e.X, e.Y, e.Direction, e.Speed, GetTurnRateDegreesPerTick(e.ScannedBotId), 4.0);
            }
            else if (distance < 100) {
                shot = GetTurningPredictiveShot(X, Y, e.X, e.Y, e.Direction, e.Speed, GetTurnRateDegreesPerTick(e.ScannedBotId), 3.0);
            }
            else if (distance < 125) {
                shot = GetTurningPredictiveShot(X, Y, e.X, e.Y, e.Direction, e.Speed, GetTurnRateDegreesPerTick(e.ScannedBotId), 2.0);
            }
            else if (distance < 175) {
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
    }

    // public override void OnBulletFired(BulletFiredEvent e) {
    //     BulletState bullet = e.Bullet;

    //     Console.WriteLine("Bullet Fired : " + bullet.BulletId);
    //     Console.WriteLine("Bullet Owner : " + bullet.OwnerId);
    //     Console.WriteLine("Bullet Power : " + bullet.Power);
    //     Console.WriteLine("Bullet X : " + bullet.X);
    //     Console.WriteLine("Bullet Y : " + bullet.Y);
    //     Console.WriteLine("Bullet Direction : " + bullet.Direction);
    //     Console.WriteLine("Bullet Speed : " + bullet.Speed);
    //     Console.WriteLine("Bullet Color : " + bullet.Color); 
    // }

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
