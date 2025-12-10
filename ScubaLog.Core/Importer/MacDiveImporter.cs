using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using ScubaLog.Core.Models;
using ScubaLog.Core.Services;

namespace ScubaLog.Core.Importers;

public class MacDiveImporter
{
    private readonly string _dbPath;

    public MacDiveImporter(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
    }

    public List<Dive> ImportAllDives()
    {
        var divesByPk    = new Dictionary<int, Dive>();
        var sitesByPk    = new Dictionary<int, DiveSite>();
        var buddiesByPk  = new Dictionary<int, Buddy>();
        var tagsByPk     = new Dictionary<int, string>();
        var tanksByDive  = new Dictionary<int, List<TankUsage>>();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        LoadSites(conn, sitesByPk);
        LoadBuddies(conn, buddiesByPk);
        LoadTags(conn, tagsByPk);
        LoadTankUsage(conn, tanksByDive);
        LoadDives(conn, divesByPk, sitesByPk, tanksByDive);

        AttachBuddiesToDives(conn, divesByPk, buddiesByPk);
        AttachTagsToDives(conn, divesByPk, tagsByPk);

        return new List<Dive>(divesByPk.Values);
    }

    // ----------------- SITES -----------------

    private static void LoadSites(SqliteConnection conn, Dictionary<int, DiveSite> sitesByPk)
    {
        const string sql = @"
            SELECT
                Z_PK,
                ZNAME,
                ZLOCATION,
                ZCOUNTRY,
                ZGPSLAT,
                ZGPSLON,
                ZWATERTYPE,
                ZDIFFICULTY,
                ZALTITUDE,
                ZNOTES
            FROM ZDIVESITE;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var pk = reader.GetInt32(0);

            var site = new DiveSite
            {
                Id          = Guid.NewGuid(),
                Name        = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Location    = reader.IsDBNull(2) ? null        : reader.GetString(2),
                Country     = reader.IsDBNull(3) ? null        : reader.GetString(3),
                Latitude    = reader.IsDBNull(4) ? null        : reader.GetDouble(4),
                Longitude   = reader.IsDBNull(5) ? null        : reader.GetDouble(5),
                WaterType   = reader.IsDBNull(6) ? null        : reader.GetString(6),
                Difficulty  = reader.IsDBNull(7) ? null        : reader.GetString(7),
                AltitudeMeters = reader.IsDBNull(8) ? null     : reader.GetDouble(8),
                Notes       = reader.IsDBNull(9) ? null        : reader.GetString(9)
            };

            sitesByPk[pk] = site;
        }
    }

    // ----------------- BUDDIES -----------------

    private static void LoadBuddies(SqliteConnection conn, Dictionary<int, Buddy> buddiesByPk)
    {
        const string sql = @"
            SELECT
                Z_PK,
                ZNAME,
                ZUUID
            FROM ZBUDDY;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var pk = reader.GetInt32(0);

            var buddy = new Buddy
            {
                Id   = Guid.NewGuid(),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ExternalId = reader.IsDBNull(2) ? null : reader.GetString(2)
            };

            buddiesByPk[pk] = buddy;
        }
    }

    private static void AttachBuddiesToDives(
        SqliteConnection conn,
        Dictionary<int, Dive> divesByPk,
        Dictionary<int, Buddy> buddiesByPk)
    {
        // Join table: Z_1RELATIONSHIPDIVE
        // Z_1RELATIONSHIPBUDDIES -> buddy PK
        // Z_5RELATIONSHIPDIVE     -> dive PK
        const string sql = @"
            SELECT
                Z_1RELATIONSHIPBUDDIES,
                Z_5RELATIONSHIPDIVE
            FROM Z_1RELATIONSHIPDIVE;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var buddyPk = reader.GetInt32(0);
            var divePk  = reader.GetInt32(1);

            if (!divesByPk.TryGetValue(divePk, out var dive))
                continue;

            if (!buddiesByPk.TryGetValue(buddyPk, out var buddy))
                continue;

            dive.Buddies.Add(buddy);
        }
    }

    // ----------------- TAGS -----------------

    private static void LoadTags(SqliteConnection conn, Dictionary<int, string> tagsByPk)
    {
        const string sql = @"
            SELECT
                Z_PK,
                ZNAME
            FROM ZTAG;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var pk   = reader.GetInt32(0);
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            tagsByPk[pk] = name;
        }
    }

    private static void AttachTagsToDives(
        SqliteConnection conn,
        Dictionary<int, Dive> divesByPk,
        Dictionary<int, string> tagsByPk)
    {
        // Join table: Z_5RELATIONSHIPTAGS
        // Z_5RELATIONSHIPDIVES -> dive PK
        // Z_17RELATIONSHIPTAGS -> tag PK
        const string sql = @"
            SELECT
                Z_5RELATIONSHIPDIVES,
                Z_17RELATIONSHIPTAGS
            FROM Z_5RELATIONSHIPTAGS;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var divePk = reader.GetInt32(0);
            var tagPk  = reader.GetInt32(1);

            if (!divesByPk.TryGetValue(divePk, out var dive))
                continue;

            if (!tagsByPk.TryGetValue(tagPk, out var tagName))
                continue;

            if (!string.IsNullOrWhiteSpace(tagName) && !dive.Tags.Contains(tagName))
                dive.Tags.Add(tagName);
        }
    }

    // ----------------- TANKS & GAS -----------------

    private static void LoadTankUsage(
        SqliteConnection conn,
        Dictionary<int, List<TankUsage>> tanksByDive)
    {
        // ZTANKANDGAS references:
        //   ZRELATIONSHIPDIVE -> dive PK
        //   ZRELATIONSHIPTANK -> tank PK (ZTANK)
        //   ZRELATIONSHIPGAS  -> gas PK  (ZGAS)
        const string sql = @"
            SELECT
                tg.ZRELATIONSHIPDIVE,
                tg.ZISDOUBLE,
                tg.ZORDER,
                tg.ZDURATION,
                tg.ZSUPPLYTYPE,
                tg.ZAIREND,
                tg.ZAIRSTART,
                tg.ZUUID,

                t.ZSIZE,
                t.ZWORKINGPRESSURE,
                t.ZNAME,
                t.ZTYPE,

                g.ZOXYGEN,
                g.ZHELIUM,
                g.ZMINPPO2,
                g.ZMAXPPO2
            FROM ZTANKANDGAS tg
            LEFT JOIN ZTANK t ON t.Z_PK = tg.ZRELATIONSHIPTANK
            LEFT JOIN ZGAS g  ON g.Z_PK = tg.ZRELATIONSHIPGAS;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var divePk = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            if (divePk is null)
                continue;

            var usage = new TankUsage
            {
                Id = Guid.NewGuid(),

                IsDouble  = !reader.IsDBNull(1) && reader.GetInt32(1) != 0,
                SortOrder = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                DurationSeconds = reader.IsDBNull(3) ? (double?)null : reader.GetDouble(3),
                SupplyType = reader.IsDBNull(4) ? null : reader.GetString(4),

                AirEndPsi   = reader.IsDBNull(5) ? (double?)null : reader.GetDouble(5),
                AirStartPsi = reader.IsDBNull(6) ? (double?)null : reader.GetDouble(6),
                ExternalId  = reader.IsDBNull(7) ? null : reader.GetString(7),

                TankSizeLiters    = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8),
                WorkingPressurePsi = reader.IsDBNull(9) ? (double?)null : reader.GetDouble(9),
                TankName          = reader.IsDBNull(10) ? null : reader.GetString(10),
                TankType          = reader.IsDBNull(11) ? null : reader.GetString(11),

                O2Percent    = reader.IsDBNull(12) ? (double?)null : reader.GetDouble(12),
                HePercent    = reader.IsDBNull(13) ? (double?)null : reader.GetDouble(13),
                MinPpo2      = reader.IsDBNull(14) ? (double?)null : reader.GetDouble(14),
                MaxPpo2      = reader.IsDBNull(15) ? (double?)null : reader.GetDouble(15)
            };

            if (!tanksByDive.TryGetValue(divePk.Value, out var list))
            {
                list = new List<TankUsage>();
                tanksByDive[divePk.Value] = list;
            }

            list.Add(usage);
        }
    }

    // ----------------- DIVES -----------------

    private static void LoadDives(
        SqliteConnection conn,
        Dictionary<int, Dive> divesByPk,
        Dictionary<int, DiveSite> sitesByPk,
        Dictionary<int, List<TankUsage>> tanksByDive)
    {
        const string sql = @"
            SELECT
                Z_PK,
                ZDIVENUMBER,
                ZRAWDATE,
                ZTOTALDURATION,
                ZMAXDEPTH,
                ZAVERAGEDEPTH,

                ZRELATIONSHIPDIVESITE,
                ZREPETITIVEDIVENUMBER,
                ZSURFACEINTERVAL,

                ZAIRTEMP,
                ZTEMPHIGH,
                ZTEMPLOW,
                ZWEATHER,
                ZCURRENT,
                ZSURFACECONDITIONS,
                ZVISIBILITY,
                ZENTRYTYPE,

                ZDECOMPRESSION,
                ZHASAIR,
                ZHASNDT,
                ZHASPPO2,
                ZHASTEMP,
                ZCNS,
                ZDECOMODEL,
                ZGASMODEL,
                ZSAMPLEINTERVAL,

                ZBOATCAPTAIN,
                ZBOATNAME,
                ZDIVEMASTER,
                ZDIVEOPERATOR,

                ZRATING,
                ZNOTES
            FROM ZDIVE;
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var pk = reader.GetInt32(0);

            var number          = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            var rawDateSeconds  = reader.IsDBNull(2) ? (double?)null : reader.GetDouble(2);
            var totalDuration   = reader.IsDBNull(3) ? (double?)null : reader.GetDouble(3);
            var maxDepth        = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4);
            var avgDepth        = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5);

            var sitePk          = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
            var repDiveNumber   = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
            var surfaceInterval = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8);

            var airTemp         = reader.IsDBNull(9)  ? (double?)null : reader.GetDouble(9);
            var tempHigh        = reader.IsDBNull(10) ? (double?)null : reader.GetDouble(10);
            var tempLow         = reader.IsDBNull(11) ? (double?)null : reader.GetDouble(11);
            var weather         = reader.IsDBNull(12) ? null : reader.GetString(12);
            var current         = reader.IsDBNull(13) ? null : reader.GetString(13);
            var surfaceCond     = reader.IsDBNull(14) ? null : reader.GetString(14);
            var visibility      = reader.IsDBNull(15) ? null : reader.GetString(15);
            var entryType       = reader.IsDBNull(16) ? null : reader.GetString(16);

            var hasDeco         = !reader.IsDBNull(17) && reader.GetInt32(17) != 0;
            var hasAir          = !reader.IsDBNull(18) && reader.GetInt32(18) != 0;
            var hasNdt          = !reader.IsDBNull(19) && reader.GetInt32(19) != 0;
            var hasPpo2         = !reader.IsDBNull(20) && reader.GetInt32(20) != 0;
            var hasTempSeries   = !reader.IsDBNull(21) && reader.GetInt32(21) != 0;
            var cns             = reader.IsDBNull(22) ? (double?)null : reader.GetDouble(22);
            var decoModel       = reader.IsDBNull(23) ? null : reader.GetString(23);
            var gasModel        = reader.IsDBNull(24) ? null : reader.GetString(24);
            var sampleInterval  = reader.IsDBNull(25) ? (double?)null : reader.GetDouble(25);

            var boatCaptain     = reader.IsDBNull(26) ? null : reader.GetString(26);
            var boatName        = reader.IsDBNull(27) ? null : reader.GetString(27);
            var divemaster      = reader.IsDBNull(28) ? null : reader.GetString(28);
            var diveOperator    = reader.IsDBNull(29) ? null : reader.GetString(29);

            var rating          = reader.IsDBNull(30) ? (double?)null : reader.GetDouble(30);
            var notes           = reader.IsDBNull(31) ? string.Empty : reader.GetString(31);

            var startTime = rawDateSeconds.HasValue
                ? FromMacAbsoluteTime(rawDateSeconds.Value)
                : DateTime.MinValue;

            var durationTs = totalDuration.HasValue
                ? TimeSpan.FromSeconds(totalDuration.Value)
                : TimeSpan.Zero;

            var dive = new Dive
            {
                Id        = Guid.NewGuid(),
                Number    = number,
                StartTime = startTime,
                Duration  = durationTs,

                MaxDepthMeters = maxDepth,
                AvgDepthMeters = avgDepth,

                // Site
                SiteId = null,
                Site   = (sitePk.HasValue && sitesByPk.TryGetValue(sitePk.Value, out var site))
                    ? site
                    : null,

                // Repetitive / SI
                RepetitiveDiveNumber  = repDiveNumber,
                SurfaceIntervalMinutes = surfaceInterval.HasValue
                    ? surfaceInterval.Value / 60.0
                    : (double?)null,

                // Environment
                AirTempC         = airTemp,
                WaterTempHighC   = tempHigh,
                WaterTempLowC    = tempLow,
                Weather          = weather,
                Current          = current,
                SurfaceConditions = surfaceCond,
                Visibility       = visibility,
                EntryType        = entryType,
                //WaterType        = waterType,

                // Deco / model
                HasDecompression   = hasDeco,
                HasAirSeries       = hasAir,
                HasNdtSeries       = hasNdt,
                HasPpo2Series      = hasPpo2,
                HasTempSeries      = hasTempSeries,
                CnsPercent         = cns,
                DecoModel          = decoModel,
                GasModel           = gasModel,
                SampleIntervalSeconds = sampleInterval,

                // Logistics
                BoatCaptain = boatCaptain,
                BoatName    = boatName,
                Divemaster  = divemaster,
                DiveOperator = diveOperator,

                // Rating / notes
                Rating = rating,
                Notes  = notes,

                // Tanks attached below
                Tanks = new List<TankUsage>(),

                // Samples: left empty for now; can be filled from ZSAMPLES/UDDF later
                Samples = new List<DiveSample>()
            };

            if (sitePk.HasValue && sitesByPk.TryGetValue(sitePk.Value, out var s))
            {
                dive.Site   = s;
                dive.SiteId = s.Id;
            }

            if (tanksByDive.TryGetValue(pk, out var tankList))
            {
                dive.Tanks.AddRange(tankList);
            }

            //TODO: TEMP: until we parse real MacDive samples, synthesize a profile
            dive.Samples = DiveLogService.GenerateSyntheticProfile(dive);
            
            divesByPk[pk] = dive;
        }
    }

    // ----------------- HELPERS -----------------

    private static DateTime FromMacAbsoluteTime(double secondsSince2001)
    {
        // Mac absolute time: seconds since Jan 1 2001 00:00:00 GMT
        var epoch = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(secondsSince2001).ToLocalTime();
    }
}