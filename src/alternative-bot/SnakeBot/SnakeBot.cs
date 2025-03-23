using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

struct Enemy {
    public bool alive;
    public double direction;
    public double bearing;
    public double distance;
}



public class SnakeBot : Bot
{   

    int l_board = 100;
    int r_board = 700;
    int u_board = 100;
    int d_board = 500;
    int clockwise = 1;
    bool running_away = false;
    bool is_shooting = false;

    Enemy[] enemies = new Enemy[4];

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

        if (Math.Abs(a) > 1e-9 && discriminant >= 0.0)
        {
            double sqrtDisc = Math.Sqrt(discriminant);
            double t1 = (-b + sqrtDisc) / (2.0 * a);
            double t2 = (-b - sqrtDisc) / (2.0 * a);

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
                t = 0.0;
            }
        }
        else
        {
            t = 0.0;
        }

        double predictedX = enemyX + vx * t;
        double predictedY = enemyY + vy * t;
        double firingAngle = Math.Atan2(predictedY - myY, predictedX - myX);
        firingAngle = firingAngle * 180 / Math.PI;

        return new double[] { bulletPower, firingAngle };
    }

    static void Main(string[] args)
    {
        new SnakeBot().Start();
    }

    SnakeBot() : base(BotInfo.FromFile("SnakeBot.json")) { }

    public override void Run()
    {
        /* Customize bot colors, read the documentation for more information */
        BodyColor = Color.FromArgb(0,0,0);

        AdjustRadarForBodyTurn = false;
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = false;
        
        double range = 1;
        int cur_enemy_count = EnemyCount;
        running_away = false;
        is_shooting = false;

        while (IsRunning)
        {
            if (!running_away && !is_shooting) {
                SetTurnGunLeft(20);
                SetTurnLeft(MaxTurnRate / range);
                SetForward(20 * clockwise);
                Go();
            }


            if (position_on_edge() && !running_away) {
                double run_angle = calcDodgeAngle();
                snakeMove(CalcBearing(run_angle), 100);
                clockwise *= -1;
            }

            if (EnemyCount < cur_enemy_count) {
                cur_enemy_count = EnemyCount;
                for (int i = 0; i < 3; i++) {
                    enemies[i].alive = false;
                }
            }
        }
    }

    double calcDodgeAngle() {
        double dodge_x = 0;
        double dodge_y = 0;
        double total_weight = 0;

        for (int i = 0; i < 4; i++) {
            if (enemies[i].alive) {
                double rad = enemies[i].direction * Math.PI /180;
                double distance = enemies[i].distance;
                double weight = 100.0 / (distance + 1.0);
                dodge_x -= Math.Cos(rad) * weight;
                dodge_y -= Math.Sin(rad) * weight;
                total_weight += weight;
            }
        }

        double boundary_weight = 70.0;
        double battlefield_width = 800;
        double battlefield_height = 600;

        double dist_to_left = X;
        double dist_to_right = battlefield_width - X;
        double dist_to_bottom = Y;
        double dist_to_top = battlefield_height - Y;

        if (dist_to_left < 100) {
            double wall_factor = (100 - dist_to_left) / 100.0;
            double wall_weight = boundary_weight * wall_factor;
            dodge_x += wall_weight;
            total_weight += wall_weight;
        }

        if (dist_to_right < 100) {
            double wall_factor = (100 - dist_to_right) / 100.0;
            double wall_weight = boundary_weight * wall_factor;
            dodge_x -= wall_weight;
            total_weight += wall_weight;
        }

        if (dist_to_bottom < 100) {
            double wall_factor = (100 - dist_to_bottom) / 100.0;
            double wall_weight = boundary_weight * wall_factor;
            dodge_y += wall_weight;
            total_weight += wall_weight;
        }

        if (dist_to_top < 100) {
            double wall_factor = (100 - dist_to_top) / 100.0;
            double wall_weight = boundary_weight * wall_factor;
            dodge_y -= wall_weight;
            total_weight += wall_weight;
        }

        if (total_weight > 0) {
            dodge_x /= total_weight;
            dodge_y /= total_weight;
        }

        double result_angle = Math.Atan2(dodge_y, dodge_x) * 180 / Math.PI;
        return (result_angle + 360) % 360;
    }

    void snakeMove(double angle, double distance) {
        if (!is_shooting) SetTurnGunLeft(10_000);
        running_away = true;

        // turn to running angle
        TurnLeft(angle);
        
        // snake movement
        double turn_needed = 90 / MaxTurnRate;
        int i;
        for (i = 0; i * MaxSpeed * turn_needed < distance; i++) {
            if (!is_shooting) SetTurnGunLeft(10_000);
            SetTurnLeft(90 * Math.Pow(-1, i));
            Forward(MaxSpeed * turn_needed);
        }

        running_away = false;
    }

    // so as to not lock in to enemy
    int shoot_id = 0;
    public override void OnScannedBot(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        double direction = DirectionTo(e.X, e.Y);
        double bearing = BearingTo(e.X, e.Y);

        Console.Write("\nbot id: " + e.ScannedBotId);
        // save enemy location
        enemies[e.ScannedBotId - 1].alive = true; 
        enemies[e.ScannedBotId - 1].direction = direction; 
        enemies[e.ScannedBotId - 1].bearing = bearing; 
        enemies[e.ScannedBotId - 1].distance = distance;


        // dodge
        if (distance < 200 && !running_away) {
            double run_angle = calcDodgeAngle();

            Console.Write("\nRun Away Angle: " + run_angle);
            snakeMove(CalcBearing(run_angle), 100);
        }

        // gun
        if (shoot_id % 2 == 1) {
            double[] shot = GetPredictedShot(X, Y, e.X, e.Y, e.Direction, e.Speed);
            double bulletPower = shot[0];
            double firingAngle = shot[1];

            double gunTurn = NormalizeRelativeAngle(firingAngle - GunDirection);

            is_shooting = true;
            if (gunTurn < 2.5) {
                SetTurnGunLeft(gunTurn);
                Fire(bulletPower);
                shoot_id = e.ScannedBotId;
                Console.Write("\nshoot bih");
            }
            is_shooting = false;
        }

        shoot_id++;
    }

    public bool position_on_edge() {
        return X <= l_board || X >= r_board || Y <= u_board || Y >= d_board;
    }

    public override void OnBotDeath(BotDeathEvent botDeathEvent) {
        running_away = false;
    }

}
