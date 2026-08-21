using System;
using UnityEngine;

[ExecuteAlways]
public class AstronomicalTimeSystem : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("ASTRONOMICAL OBJECTS")]

    [SerializeField]
    private Transform _Sun;

    [SerializeField]
    private Transform _Moon;


    // ============================================================
    // LOCATION
    // ============================================================

    [Header("OBSERVER LOCATION")]

    [Tooltip("Latitud. Norte positivo, Sur negativo.")]
    [Range(-90f, 90f)]
    [SerializeField]
    private float _Latitude = 6.2442f;

    [Tooltip("Longitud. Este positivo, Oeste negativo.")]
    [Range(-180f, 180f)]
    [SerializeField]
    private float _Longitude = -75.5812f;


    // ============================================================
    // TIME MODE
    // ============================================================

    public enum TimeMode
    {
        RealTime,
        Manual,
        Simulated
    }

    [Header("TIME MODE")]

    [SerializeField]
    private TimeMode _TimeMode = TimeMode.RealTime;


    // ============================================================
    // MANUAL DATE
    // ============================================================

    [Header("MANUAL DATE")]

    [SerializeField]
    private int _Year = 2026;

    [Range(1, 12)]
    [SerializeField]
    private int _Month = 8;

    [Range(1, 31)]
    [SerializeField]
    private int _Day = 20;

    [Range(0, 23)]
    [SerializeField]
    private int _Hour = 12;

    [Range(0, 59)]
    [SerializeField]
    private int _Minute = 0;

    [Range(0, 59)]
    [SerializeField]
    private int _Second = 0;


    // ============================================================
    // SIMULATION
    // ============================================================

    [Header("SIMULATION")]

    [Tooltip(
        "Velocidad del tiempo.\n" +
        "1 = tiempo real\n" +
        "60 = 1 minuto por segundo\n" +
        "3600 = 1 hora por segundo\n" +
        "86400 = 1 día por segundo"
    )]
    [SerializeField]
    private double _TimeScale = 60.0;


    // ============================================================
    // ROTATION OFFSETS
    // ============================================================

    [Header("ROTATION OFFSETS")]

    [SerializeField]
    private Vector3 _SunRotationOffset;

    [SerializeField]
    private Vector3 _MoonRotationOffset;


    // ============================================================
    // SKYBOX / STARS
    // ============================================================

    [Header("SKYBOX / STARS")]

    [Tooltip("Actualiza las estrellas según el tiempo sideral local.")]
    [SerializeField]
    private bool _UpdateSkyboxStars = true;

    [Tooltip("Multiplicador opcional. 1 = velocidad astronómica real.")]
    [SerializeField]
    private double _StarSpeedMultiplier = 1.0;


    // ============================================================
    // DEBUG
    // ============================================================

    [Header("DEBUG")]

    [SerializeField]
    private bool _DrawGizmos = true;

    [SerializeField]
    private bool _DisplayDebugInfo = true;


    // ============================================================
    // CURRENT TIME
    // ============================================================

    [Header("CURRENT TIME")]

    [SerializeField]
    private string _CurrentLocalTime;

    [SerializeField]
    private string _CurrentUTCTime;


    // ============================================================
    // SUN
    // ============================================================

    [Header("SUN")]

    [SerializeField]
    private double _SunAltitude;

    [SerializeField]
    private double _SunAzimuth;


    // ============================================================
    // MOON
    // ============================================================

    [Header("MOON")]

    [SerializeField]
    private double _MoonAltitude;

    [SerializeField]
    private double _MoonAzimuth;


    // ============================================================
    // STARS
    // ============================================================

    [Header("STARS")]

    [SerializeField]
    private double _LocalSiderealTime;

    [SerializeField]
    private double _StarLatitude;


    // ============================================================
    // INTERNAL
    // ============================================================

    private DateTime _CurrentUTC;

    private bool _initialized;

    private double _lastLatitude;
    private double _lastLongitude;

    private int _lastYear;
    private int _lastMonth;
    private int _lastDay;
    private int _lastHour;
    private int _lastMinute;
    private int _lastSecond;


    // ============================================================
    // SHADER IDs
    // ============================================================

    private static readonly int SunDirID =
        Shader.PropertyToID("_Sun_Direction");

    private static readonly int MoonDirID =
        Shader.PropertyToID("_Moon_Direction");

    private static readonly int MoonSpaceMatrixID =
        Shader.PropertyToID("_Moon_Space_Matrix");

    private static readonly int StarLatitudeID =
        Shader.PropertyToID("_Star_Latitude");

    private static readonly int StarSiderealTimeID =
        Shader.PropertyToID("_Star_Sidereal_Time");


    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        Initialize();
    }


    private void Start()
    {
        Initialize();
    }


    private void Update()
    {
        if (!_initialized)
            Initialize();

        UpdateTime();
        UpdateAstronomy();
    }


    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Initialize()
    {
        if (_TimeMode == TimeMode.RealTime)
        {
            _CurrentUTC = DateTime.UtcNow;
        }
        else
        {
            _CurrentUTC = CreateManualDate();
        }

        _initialized = true;

        CacheManualValues();

        UpdateAstronomy();
    }


    // ============================================================
    // TIME
    // ============================================================

    private void UpdateTime()
    {
        switch (_TimeMode)
        {
            case TimeMode.RealTime:

                _CurrentUTC = DateTime.UtcNow;

                break;


            case TimeMode.Manual:

                if (ManualValuesChanged())
                {
                    _CurrentUTC = CreateManualDate();

                    CacheManualValues();
                }

                break;


            case TimeMode.Simulated:

                _CurrentUTC =
                    _CurrentUTC.AddSeconds(
                        _TimeScale * Time.deltaTime
                    );

                break;
        }
    }


    // ============================================================
    // MANUAL DATE
    // ============================================================

    private DateTime CreateManualDate()
    {
        int year =
            Mathf.Clamp(
                _Year,
                1,
                9999
            );

        int month =
            Mathf.Clamp(
                _Month,
                1,
                12
            );

        int maxDay =
            DateTime.DaysInMonth(
                year,
                month
            );

        int day =
            Mathf.Clamp(
                _Day,
                1,
                maxDay
            );

        int hour =
            Mathf.Clamp(
                _Hour,
                0,
                23
            );

        int minute =
            Mathf.Clamp(
                _Minute,
                0,
                59
            );

        int second =
            Mathf.Clamp(
                _Second,
                0,
                59
            );

        return new DateTime(
            year,
            month,
            day,
            hour,
            minute,
            second,
            DateTimeKind.Utc
        );
    }


    // ============================================================
    // DETECT MANUAL CHANGES
    // ============================================================

    private bool ManualValuesChanged()
    {
        return
            _Year != _lastYear ||
            _Month != _lastMonth ||
            _Day != _lastDay ||
            _Hour != _lastHour ||
            _Minute != _lastMinute ||
            _Second != _lastSecond ||

            !Mathf.Approximately(
                _Latitude,
                (float)_lastLatitude
            ) ||

            !Mathf.Approximately(
                _Longitude,
                (float)_lastLongitude
            );
    }


    // ============================================================
    // CACHE
    // ============================================================

    private void CacheManualValues()
    {
        _lastYear = _Year;
        _lastMonth = _Month;
        _lastDay = _Day;
        _lastHour = _Hour;
        _lastMinute = _Minute;
        _lastSecond = _Second;

        _lastLatitude = _Latitude;
        _lastLongitude = _Longitude;
    }


    // ============================================================
    // ASTRONOMY
    // ============================================================

    private void UpdateAstronomy()
    {
        if (_Sun == null ||
            _Moon == null)
        {
            return;
        }


        double julianDate =
            CalculateJulianDate(
                _CurrentUTC
            );


        double siderealTime =
            CalculateLocalSiderealTime(
                julianDate,
                _Longitude
            );


        _LocalSiderealTime =
            siderealTime;

        _StarLatitude =
            _Latitude;


        // ========================================================
        // SUN
        // ========================================================

        EquatorialCoordinates sun =
            CalculateSunPosition(
                julianDate
            );


        HorizontalCoordinates sunHorizontal =
            EquatorialToHorizontal(
                sun.RightAscension,
                sun.Declination,
                _Latitude,
                siderealTime
            );


        _SunAltitude =
            sunHorizontal.Altitude;

        _SunAzimuth =
            sunHorizontal.Azimuth;


        // ========================================================
        // MOON
        // ========================================================

        EquatorialCoordinates moon =
            CalculateMoonPosition(
                julianDate
            );


        HorizontalCoordinates moonHorizontal =
            EquatorialToHorizontal(
                moon.RightAscension,
                moon.Declination,
                _Latitude,
                siderealTime
            );


        _MoonAltitude =
            moonHorizontal.Altitude;

        _MoonAzimuth =
            moonHorizontal.Azimuth;


        // ========================================================
        // UNITY DIRECTIONS
        // ========================================================

        Vector3 sunDirection =
            HorizontalToUnityDirection(
                _SunAltitude,
                _SunAzimuth
            );


        Vector3 moonDirection =
            HorizontalToUnityDirection(
                _MoonAltitude,
                _MoonAzimuth
            );


        // ========================================================
        // TRANSFORMS
        // ========================================================

        UpdateSunTransform(
            sunDirection
        );

        UpdateMoonTransform(
            moonDirection
        );


        // ========================================================
        // SKYBOX
        // ========================================================

        UpdateSkyboxGlobals(
            sunDirection,
            moonDirection,
            siderealTime
        );


        // ========================================================
        // DEBUG
        // ========================================================

        if (_DisplayDebugInfo)
        {
            _CurrentUTCTime =
                _CurrentUTC.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );

            _CurrentLocalTime =
                _CurrentUTC
                    .ToLocalTime()
                    .ToString(
                        "yyyy-MM-dd HH:mm:ss"
                    );
        }
        else
        {
            _CurrentUTCTime = "";
            _CurrentLocalTime = "";
        }
    }


    // ============================================================
    // SKYBOX GLOBALS
    // ============================================================

    private void UpdateSkyboxGlobals(
        Vector3 sunDirection,
        Vector3 moonDirection,
        double siderealTime)
    {
        Shader.SetGlobalVector(
            SunDirID,
            -sunDirection
        );


        Shader.SetGlobalVector(
            MoonDirID,
            -moonDirection
        );


        if (_Moon != null)
        {
            Matrix4x4 moonMatrix =
                new Matrix4x4(
                    -_Moon.forward,
                    _Moon.up,
                    -_Moon.right,
                    Vector4.zero
                ).transpose;

            Shader.SetGlobalMatrix(
                MoonSpaceMatrixID,
                moonMatrix
            );
        }


        if (_UpdateSkyboxStars)
        {
            Shader.SetGlobalFloat(
                StarLatitudeID,
                (float)_StarLatitude
            );


            double normalizedSiderealTime =
                NormalizeDegrees(
                    siderealTime
                ) / 360.0;


            Shader.SetGlobalFloat(
                StarSiderealTimeID,
                (float)normalizedSiderealTime
            );
        }
    }


    // ============================================================
    // SUN TRANSFORM
    // ============================================================

    private void UpdateSunTransform(
        Vector3 direction)
    {
        Quaternion rotation =
            Quaternion.LookRotation(
                -direction,
                Vector3.up
            );

        rotation *=
            Quaternion.Euler(
                _SunRotationOffset
            );

        _Sun.rotation =
            rotation;
    }


    // ============================================================
    // MOON TRANSFORM
    // ============================================================

    private void UpdateMoonTransform(
        Vector3 direction)
    {
        Quaternion rotation =
            Quaternion.LookRotation(
                -direction,
                Vector3.up
            );

        rotation *=
            Quaternion.Euler(
                _MoonRotationOffset
            );

        _Moon.rotation =
            rotation;
    }


    // ============================================================
    // JULIAN DATE
    // ============================================================

    private double CalculateJulianDate(
        DateTime date)
    {
        int year =
            date.Year;

        int month =
            date.Month;

        double day =
            date.Day
            +
            date.Hour / 24.0
            +
            date.Minute / 1440.0
            +
            date.Second / 86400.0
            +
            date.Millisecond / 86400000.0;


        if (month <= 2)
        {
            year--;
            month += 12;
        }


        int A =
            year / 100;

        int B =
            2 -
            A +
            A / 4;


        return
            Math.Floor(
                365.25 *
                (year + 4716)
            )
            +
            Math.Floor(
                30.6001 *
                (month + 1)
            )
            +
            day
            +
            B
            -
            1524.5;
    }


    // ============================================================
    // SUN
    // ============================================================

    private EquatorialCoordinates CalculateSunPosition(
        double jd)
    {
        double n =
            jd -
            2451545.0;


        double L =
            NormalizeDegrees(
                280.460 +
                0.9856474 * n
            );


        double g =
            NormalizeDegrees(
                357.528 +
                0.9856003 * n
            );


        double gRad =
            DegreesToRadians(g);


        double lambda =
            L
            +
            1.915 *
            Math.Sin(gRad)
            +
            0.020 *
            Math.Sin(
                2.0 * gRad
            );


        lambda =
            NormalizeDegrees(
                lambda
            );


        double epsilon =
            23.439 -
            0.0000004 * n;


        double lambdaRad =
            DegreesToRadians(lambda);

        double epsilonRad =
            DegreesToRadians(epsilon);


        double rightAscension =
            Math.Atan2(
                Math.Cos(epsilonRad) *
                Math.Sin(lambdaRad),
                Math.Cos(lambdaRad)
            );


        double declination =
            Math.Asin(
                Math.Sin(epsilonRad) *
                Math.Sin(lambdaRad)
            );


        return new EquatorialCoordinates
        {
            RightAscension =
                NormalizeDegrees(
                    RadiansToDegrees(
                        rightAscension
                    )
                ),

            Declination =
                RadiansToDegrees(
                    declination
                )
        };
    }


    // ============================================================
    // MOON
    // ============================================================

    private EquatorialCoordinates CalculateMoonPosition(
        double jd)
    {
        double d =
            jd -
            2451543.5;


        double N =
            NormalizeDegrees(
                125.1228 -
                0.0529538083 * d
            );


        double i =
            5.1454;


        double w =
            NormalizeDegrees(
                318.0634 +
                0.1643573223 * d
            );


        double a =
            60.2666;


        double e =
            0.054900;


        double M =
            NormalizeDegrees(
                115.3654 +
                13.0649929509 * d
            );


        double E =
            SolveKepler(
                DegreesToRadians(M),
                e
            );


        double xv =
            a *
            (
                Math.Cos(E) -
                e
            );


        double yv =
            a *
            Math.Sqrt(
                1 -
                e * e
            ) *
            Math.Sin(E);


        double v =
            Math.Atan2(
                yv,
                xv
            );


        double r =
            Math.Sqrt(
                xv * xv +
                yv * yv
            );


        double NRad =
            DegreesToRadians(N);

        double iRad =
            DegreesToRadians(i);

        double wRad =
            DegreesToRadians(w);


        double xh =
            r *
            (
                Math.Cos(NRad) *
                Math.Cos(v + wRad)
                -
                Math.Sin(NRad) *
                Math.Sin(v + wRad) *
                Math.Cos(iRad)
            );


        double yh =
            r *
            (
                Math.Sin(NRad) *
                Math.Cos(v + wRad)
                +
                Math.Cos(NRad) *
                Math.Sin(v + wRad) *
                Math.Cos(iRad)
            );


        double zh =
            r *
            Math.Sin(v + wRad) *
            Math.Sin(iRad);


        double epsilon =
            DegreesToRadians(
                23.4393
            );


        double xe =
            xh;

        double ye =
            yh *
            Math.Cos(epsilon)
            -
            zh *
            Math.Sin(epsilon);

        double ze =
            yh *
            Math.Sin(epsilon)
            +
            zh *
            Math.Cos(epsilon);


        double rightAscension =
            Math.Atan2(
                ye,
                xe
            );


        double declination =
            Math.Atan2(
                ze,
                Math.Sqrt(
                    xe * xe +
                    ye * ye
                )
            );


        return new EquatorialCoordinates
        {
            RightAscension =
                NormalizeDegrees(
                    RadiansToDegrees(
                        rightAscension
                    )
                ),

            Declination =
                RadiansToDegrees(
                    declination
                )
        };
    }


    // ============================================================
    // EQUATORIAL -> HORIZONTAL
    // ============================================================

    private HorizontalCoordinates
        EquatorialToHorizontal(
            double rightAscension,
            double declination,
            double latitude,
            double siderealTime)
    {
        double hourAngle =
            NormalizeDegrees(
                siderealTime -
                rightAscension
            );


        if (hourAngle > 180)
            hourAngle -= 360;


        double H =
            DegreesToRadians(
                hourAngle
            );


        double dec =
            DegreesToRadians(
                declination
            );


        double lat =
            DegreesToRadians(
                latitude
            );


        double sinAltitude =
            Math.Sin(lat) *
            Math.Sin(dec)
            +
            Math.Cos(lat) *
            Math.Cos(dec) *
            Math.Cos(H);


        sinAltitude =
            Mathf.Clamp(
                (float)sinAltitude,
                -1f,
                1f
            );


        double altitude =
            RadiansToDegrees(
                Math.Asin(
                    sinAltitude
                )
            );


        double y =
            Math.Sin(H);


        double x =
            Math.Cos(H) *
            Math.Sin(lat)
            -
            Math.Tan(dec) *
            Math.Cos(lat);


        double azimuth =
            RadiansToDegrees(
                Math.Atan2(
                    y,
                    x
                )
            );


        azimuth =
            NormalizeDegrees(
                azimuth + 180
            );


        return new HorizontalCoordinates
        {
            Altitude = altitude,
            Azimuth = azimuth
        };
    }


    // ============================================================
    // HORIZONTAL -> UNITY
    // ============================================================

    private Vector3 HorizontalToUnityDirection(
        double altitude,
        double azimuth)
    {
        double alt =
            DegreesToRadians(
                altitude
            );


        double azi =
            DegreesToRadians(
                azimuth
            );


        float x =
            (float)
            (
                Math.Cos(alt) *
                Math.Sin(azi)
            );


        float y =
            (float)
            Math.Sin(alt);


        float z =
            (float)
            (
                Math.Cos(alt) *
                Math.Cos(azi)
            );


        return new Vector3(
            x,
            y,
            z
        ).normalized;
    }


    // ============================================================
    // LOCAL SIDEREAL TIME
    // ============================================================

    private double CalculateLocalSiderealTime(
        double jd,
        double longitude)
    {
        double T =
            (
                jd -
                2451545.0
            )
            /
            36525.0;


        double gmst =
            280.46061837
            +
            360.98564736629 *
            (
                jd -
                2451545.0
            )
            +
            0.000387933 *
            T * T
            -
            T * T * T /
            38710000.0;


        return NormalizeDegrees(
            gmst +
            longitude
        );
    }


    // ============================================================
    // KEPLER
    // ============================================================

    private double SolveKepler(
        double M,
        double eccentricity)
    {
        double E = M;


        for (int i = 0; i < 12; i++)
        {
            double f =
                E -
                eccentricity *
                Math.Sin(E) -
                M;


            double fp =
                1 -
                eccentricity *
                Math.Cos(E);


            E -=
                f / fp;
        }


        return E;
    }


    // ============================================================
    // MATH
    // ============================================================

    private double NormalizeDegrees(
        double value)
    {
        value %= 360.0;

        if (value < 0)
            value += 360.0;

        return value;
    }


    private double DegreesToRadians(
        double degrees)
    {
        return
            degrees *
            Math.PI /
            180.0;
    }


    private double RadiansToDegrees(
        double radians)
    {
        return
            radians *
            180.0 /
            Math.PI;
    }


    // ============================================================
    // DATA STRUCTURES
    // ============================================================

    private struct EquatorialCoordinates
    {
        public double RightAscension;
        public double Declination;
    }


    private struct HorizontalCoordinates
    {
        public double Altitude;
        public double Azimuth;
    }


    // ============================================================
    // GIZMOS
    // ============================================================

    private void OnDrawGizmos()
    {
        if (!_DrawGizmos)
            return;


        if (_Sun != null)
        {
            Gizmos.color =
                Color.yellow;

            Gizmos.DrawRay(
                transform.position,
                -_Sun.forward * 10f
            );
        }


        if (_Moon != null)
        {
            Gizmos.color =
                Color.white;

            Gizmos.DrawRay(
                transform.position,
                -_Moon.forward * 10f
            );
        }
    }
}