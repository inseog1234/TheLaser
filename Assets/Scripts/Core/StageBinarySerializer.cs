using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Core
{
    public static class StageBinarySerializer
    {
        private const string Magic = "TLS_STAGE";
        private const int Version = 7;

        public static void Save(StageData stageData, string path)
        {
            if (stageData == null)
                throw new ArgumentNullException(nameof(stageData));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new BinaryWriter(fileStream);

            writer.Write(Magic);
            writer.Write(Version);

            writer.Write(stageData.stageNumber);
            writer.Write(stageData.stageName ?? string.Empty);
            writer.Write(stageData.chapterIndex);
            writer.Write(stageData.chapterName ?? string.Empty);
            writer.Write(stageData.stageIndexInChapter);
            writer.Write(stageData.chapterFeatureName ?? string.Empty);
            writer.Write(stageData.bgmEventPath ?? string.Empty);
            writer.Write(stageData.hasTutorial);
            WriteStringList(writer, stageData.tutorialPages);
            WriteVector2Int(writer, stageData.clearHolePosition);
            writer.Write(stageData.width);
            writer.Write(stageData.height);
            writer.Write(stageData.useLaserDistanceLimit);
            writer.Write(stageData.laserMaxDistance);
            writer.Write(stageData.moveLimit);
            WriteVector2Int(writer, stageData.playerStartPosition);
            writer.Write((int)stageData.playerStartDirection);
            WriteVector2IntList(writer, stageData.playerRoutePositions);

            WriteVector2IntList(writer, stageData.wallPositions);
            WriteVector2IntList(writer, stageData.targetPositions);
            WriteTargetList(writer, stageData.advancedTargets);
            WriteIntList(writer, stageData.sequenceLockPattern);
            WriteDistanceSensorList(writer, stageData.distanceSensors);
            WriteTransformZoneList(writer, stageData.transformZones);
            WriteObjectList(writer, stageData.objects);
            WriteSolutionActionList(writer, stageData.solutionActions);
        }

        public static StageData Load(string path)
        {
            using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new BinaryReader(fileStream);
            return Read(reader, path);
        }

        public static StageData Load(byte[] bytes, string sourceName = "Memory")
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using MemoryStream memoryStream = new MemoryStream(bytes);
            using BinaryReader reader = new BinaryReader(memoryStream);
            return Read(reader, sourceName);
        }

        private static StageData Read(BinaryReader reader, string sourceName)
        {
            string magic = reader.ReadString();
            if (magic != Magic)
                throw new InvalidDataException($"The file is not a The Laser stage file: {sourceName}");

            int version = reader.ReadInt32();
            if (version < 1 || version > Version)
                throw new InvalidDataException($"Unsupported stage file version: {version}");

            StageData stageData = new StageData
            {
                stageNumber = reader.ReadInt32(),
                stageName = reader.ReadString()
            };

            if (version >= 5)
            {
                stageData.chapterIndex = reader.ReadInt32();
                stageData.chapterName = reader.ReadString();
                stageData.stageIndexInChapter = reader.ReadInt32();
                stageData.chapterFeatureName = reader.ReadString();
                stageData.bgmEventPath = reader.ReadString();
                stageData.hasTutorial = reader.ReadBoolean();
                stageData.tutorialPages = ReadStringList(reader);
                stageData.clearHolePosition = ReadVector2Int(reader);
            }
            else
            {
                stageData.chapterIndex = 1;
                stageData.chapterName = "Chapter 1";
                stageData.stageIndexInChapter = stageData.stageNumber;
                stageData.chapterFeatureName = string.Empty;
                stageData.bgmEventPath = string.Empty;
                stageData.hasTutorial = false;
                stageData.tutorialPages = new List<string>();
                stageData.clearHolePosition = Vector2Int.zero;
            }

            stageData.width = reader.ReadInt32();
            stageData.height = reader.ReadInt32();
            stageData.useLaserDistanceLimit = reader.ReadBoolean();
            stageData.laserMaxDistance = reader.ReadInt32();
            stageData.moveLimit = reader.ReadInt32();
            stageData.playerStartPosition = ReadVector2Int(reader);
            stageData.playerStartDirection = (GridDirection)reader.ReadInt32();
            stageData.playerRoutePositions = version >= 7 ? ReadVector2IntList(reader) : new List<Vector2Int>();
            stageData.wallPositions = ReadVector2IntList(reader);
            stageData.targetPositions = ReadVector2IntList(reader);
            stageData.advancedTargets = ReadTargetList(reader, version);
            stageData.sequenceLockPattern = ReadIntList(reader);
            stageData.distanceSensors = ReadDistanceSensorList(reader, version);
            stageData.transformZones = ReadTransformZoneList(reader, version);
            stageData.objects = ReadObjectList(reader);
            stageData.solutionActions = version >= 6 ? ReadSolutionActionList(reader) : new List<StageSolutionActionData>();

            return stageData;
        }

        public static bool TryLoad(string path, out StageData stageData)
        {
            try
            {
                stageData = Load(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[StageBinarySerializer] Load failed: {exception.Message}");
                stageData = null;
                return false;
            }
        }

        public static bool TryLoad(byte[] bytes, out StageData stageData, string sourceName = "Memory")
        {
            try
            {
                stageData = Load(bytes, sourceName);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[StageBinarySerializer] Load failed: {exception.Message}");
                stageData = null;
                return false;
            }
        }

        private static void WriteVector2Int(BinaryWriter writer, Vector2Int value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        private static Vector2Int ReadVector2Int(BinaryReader reader)
        {
            return new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
        }

        private static void WriteVector2IntList(BinaryWriter writer, List<Vector2Int> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteVector2Int(writer, list[i]);
        }

        private static List<Vector2Int> ReadVector2IntList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<Vector2Int> list = new List<Vector2Int>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadVector2Int(reader));
            return list;
        }

        private static void WriteIntList(BinaryWriter writer, List<int> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                writer.Write(list[i]);
        }

        private static List<int> ReadIntList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<int> list = new List<int>(count);
            for (int i = 0; i < count; i++)
                list.Add(reader.ReadInt32());
            return list;
        }

        private static void WriteStringList(BinaryWriter writer, List<string> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                writer.Write(list[i] ?? string.Empty);
        }

        private static List<string> ReadStringList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<string> list = new List<string>(count);
            for (int i = 0; i < count; i++)
                list.Add(reader.ReadString());
            return list;
        }

        private static void WriteObjectList(BinaryWriter writer, List<StageObjectData> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteObject(writer, list[i]);
        }

        private static List<StageObjectData> ReadObjectList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<StageObjectData> list = new List<StageObjectData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadObject(reader));
            return list;
        }

        private static void WriteObject(BinaryWriter writer, StageObjectData data)
        {
            writer.Write(data != null);
            if (data == null)
                return;

            writer.Write((int)data.objectType);
            writer.Write((int)data.manipulationType);
            WriteVector2Int(writer, data.position);
            writer.Write((int)data.direction);
            writer.Write((int)data.mirrorShape);
            writer.Write((int)data.prismType);
            writer.Write((int)data.splitterMode);
            writer.Write((int)data.prismColor);
            writer.Write((int)data.refractionMode);
            writer.Write((int)data.lensType);
            writer.Write(data.distanceBoost);
        }

        private static StageObjectData ReadObject(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
                return null;

            return new StageObjectData
            {
                objectType = (PuzzleObjectType)reader.ReadInt32(),
                manipulationType = (ManipulationType)reader.ReadInt32(),
                position = ReadVector2Int(reader),
                direction = (GridDirection)reader.ReadInt32(),
                mirrorShape = (MirrorShape)reader.ReadInt32(),
                prismType = (PrismType)reader.ReadInt32(),
                splitterMode = (PrismSplitterMode)reader.ReadInt32(),
                prismColor = (LaserColorKind)reader.ReadInt32(),
                refractionMode = (RefractionMode)reader.ReadInt32(),
                lensType = (LensType)reader.ReadInt32(),
                distanceBoost = reader.ReadInt32()
            };
        }

        private static void WriteTargetList(BinaryWriter writer, List<StageTargetData> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteTarget(writer, list[i]);
        }

        private static List<StageTargetData> ReadTargetList(BinaryReader reader, int version)
        {
            int count = reader.ReadInt32();
            List<StageTargetData> list = new List<StageTargetData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadTarget(reader, version));
            return list;
        }

        private static void WriteTarget(BinaryWriter writer, StageTargetData data)
        {
            writer.Write(data != null);
            if (data == null)
                return;

            writer.Write(data.targetId ?? string.Empty);
            writer.Write((int)data.targetType);
            WriteVector2Int(writer, data.position);
            writer.Write((int)data.requiredColor);
            writer.Write(data.sequenceValue);
            writer.Write(data.detectionRadius);
            writer.Write(Mathf.Clamp(data.requiredIntersectionCount, 2, 3));
            WriteLaserColorList(writer, data.intersectionColors);
            writer.Write(data.requireDifferentColors);
            writer.Write(data.stopLaserOnHit);
        }

        private static StageTargetData ReadTarget(BinaryReader reader, int version)
        {
            if (!reader.ReadBoolean())
                return null;

            StageTargetData data = new StageTargetData
            {
                targetId = reader.ReadString(),
                targetType = (TargetType)reader.ReadInt32(),
                position = ReadVector2Int(reader),
                requiredColor = (LaserColorKind)reader.ReadInt32(),
                sequenceValue = reader.ReadInt32(),
                detectionRadius = reader.ReadSingle()
            };

            if (version >= 2)
            {
                data.requiredIntersectionCount = reader.ReadInt32();
                data.intersectionColors = ReadLaserColorList(reader);
            }
            else
            {
                data.requiredIntersectionCount = 2;
                data.intersectionColors = new List<LaserColorKind>();
            }

            data.requireDifferentColors = reader.ReadBoolean();
            data.stopLaserOnHit = reader.ReadBoolean();
            return data;
        }

        private static void WriteDistanceSensorList(BinaryWriter writer, List<DistanceSensorData> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteDistanceSensor(writer, list[i]);
        }

        private static List<DistanceSensorData> ReadDistanceSensorList(BinaryReader reader, int version)
        {
            int count = reader.ReadInt32();
            List<DistanceSensorData> list = new List<DistanceSensorData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadDistanceSensor(reader, version));
            return list;
        }

        private static void WriteDistanceSensor(BinaryWriter writer, DistanceSensorData data)
        {
            writer.Write(data != null);
            if (data == null)
                return;

            writer.Write(data.sensorId ?? string.Empty);
            WriteVector2Int(writer, data.position);
            writer.Write(data.detectionRadius);
            writer.Write(data.activateTransformZone);
            writer.Write(data.transformZoneId ?? string.Empty);
            WriteDistanceSensorTriggerList(writer, data.triggers);
        }

        private static DistanceSensorData ReadDistanceSensor(BinaryReader reader, int version)
        {
            if (!reader.ReadBoolean())
                return null;

            DistanceSensorData data = new DistanceSensorData
            {
                sensorId = reader.ReadString(),
                position = ReadVector2Int(reader),
                detectionRadius = reader.ReadSingle(),
                activateTransformZone = reader.ReadBoolean(),
                transformZoneId = reader.ReadString(),
                triggers = new List<DistanceSensorTriggerData>()
            };

            if (version >= 2)
                data.triggers = ReadDistanceSensorTriggerList(reader, version);

            return data;
        }

        private static void WriteTransformZoneList(BinaryWriter writer, List<TransformZoneData> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteTransformZone(writer, list[i]);
        }

        private static List<TransformZoneData> ReadTransformZoneList(BinaryReader reader, int version)
        {
            int count = reader.ReadInt32();
            List<TransformZoneData> list = new List<TransformZoneData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadTransformZone(reader, version));
            return list;
        }

        private static void WriteTransformZone(BinaryWriter writer, TransformZoneData data)
        {
            writer.Write(data != null);
            if (data == null)
                return;

            writer.Write(data.zoneId ?? string.Empty);
            WriteVector2Int(writer, data.center);
            writer.Write(data.width);
            writer.Write(data.height);
            writer.Write(data.offsetX);
            writer.Write(data.offsetY);
            writer.Write((int)data.zoneType);
            writer.Write(data.clockwise);
            writer.Write((int)data.mirrorAxis);
        }

        private static TransformZoneData ReadTransformZone(BinaryReader reader, int version)
        {
            if (!reader.ReadBoolean())
                return null;

            TransformZoneData data = new TransformZoneData
            {
                zoneId = reader.ReadString(),
                center = ReadVector2Int(reader),
                width = reader.ReadInt32(),
                height = reader.ReadInt32()
            };

            if (version >= 3)
            {
                data.offsetX = reader.ReadInt32();
                data.offsetY = reader.ReadInt32();
            }
            else
            {
                data.offsetX = -1;
                data.offsetY = -1;
            }

            data.zoneType = (TransformZoneType)reader.ReadInt32();
            data.clockwise = reader.ReadBoolean();
            data.mirrorAxis = (MirrorAxis)reader.ReadInt32();
            return data;
        }

        private static void WriteLaserColorList(BinaryWriter writer, List<LaserColorKind> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                writer.Write((int)list[i]);
        }

        private static List<LaserColorKind> ReadLaserColorList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<LaserColorKind> list = new List<LaserColorKind>(count);
            for (int i = 0; i < count; i++)
                list.Add((LaserColorKind)reader.ReadInt32());
            return list;
        }

        private static void WriteDistanceSensorTriggerList(BinaryWriter writer, List<DistanceSensorTriggerData> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteDistanceSensorTrigger(writer, list[i]);
        }

        private static List<DistanceSensorTriggerData> ReadDistanceSensorTriggerList(BinaryReader reader, int version)
        {
            int count = reader.ReadInt32();
            List<DistanceSensorTriggerData> list = new List<DistanceSensorTriggerData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadDistanceSensorTrigger(reader, version));
            return list;
        }

        private static void WriteDistanceSensorTrigger(BinaryWriter writer, DistanceSensorTriggerData data)
        {
            writer.Write(data != null);
            if (data == null)
                return;

            writer.Write(data.triggerId ?? string.Empty);
            writer.Write((int)data.triggerKind);
            WriteVector2Int(writer, data.wallPosition);
            WriteVector2Int(writer, data.wallMoveTargetPosition);
            WriteVector2Int(writer, data.prismPosition);
            writer.Write((int)data.prismDirection);
            WriteVector2Int(writer, data.mirrorPosition);
            writer.Write((int)data.mirrorDirection);
            writer.Write((int)data.mirrorShape);
            writer.Write(data.transformZoneId ?? string.Empty);
        }

        private static DistanceSensorTriggerData ReadDistanceSensorTrigger(BinaryReader reader, int version)
        {
            if (!reader.ReadBoolean())
                return null;

            DistanceSensorTriggerData data = new DistanceSensorTriggerData
            {
                triggerId = reader.ReadString(),
                triggerKind = (DistanceSensorTriggerKind)reader.ReadInt32(),
                wallPosition = ReadVector2Int(reader),
                wallMoveTargetPosition = ReadVector2Int(reader),
                prismPosition = ReadVector2Int(reader),
                prismDirection = (GridDirection)reader.ReadInt32()
            };

            if (version >= 4)
            {
                data.mirrorPosition = ReadVector2Int(reader);
                data.mirrorDirection = (GridDirection)reader.ReadInt32();
                data.mirrorShape = (MirrorShape)reader.ReadInt32();
            }
            else
            {
                data.mirrorPosition = data.prismPosition;
                data.mirrorDirection = data.prismDirection;
                data.mirrorShape = MirrorShape.NormalL;
            }

            data.transformZoneId = reader.ReadString();
            return data;
        }

        private static void WriteSolutionActionList(BinaryWriter writer, List<StageSolutionActionData> list)
        {
            writer.Write(list?.Count ?? 0);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
                WriteSolutionAction(writer, list[i]);
        }

        private static List<StageSolutionActionData> ReadSolutionActionList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<StageSolutionActionData> list = new List<StageSolutionActionData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadSolutionAction(reader));
            return list;
        }

        private static void WriteSolutionAction(BinaryWriter writer, StageSolutionActionData data)
        {
            writer.Write(data != null);
            if (data == null)
                return;

            writer.Write((int)data.actionType);
            writer.Write((int)data.direction);
        }

        private static StageSolutionActionData ReadSolutionAction(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
                return null;

            return new StageSolutionActionData
            {
                actionType = (StageSolutionActionType)reader.ReadInt32(),
                direction = (GridDirection)reader.ReadInt32()
            };
        }

    }
}
