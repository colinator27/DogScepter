using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Assets
{
    public class AssetObject : Asset
    {
        /// <summary>
        /// Contains asset object physics properties. All properties will only be used if <see cref="Physics"/> is enabled.
        /// </summary>
        public record struct PhysicsProperties
        {
            /// <summary>
            /// Whether the asset object uses GameMaker's builtin physics engine.
            /// </summary>
            /// <remarks>The default value in GameMaker for this is <see langword="false"/>.</remarks>
            public bool IsEnabled;

            /// <summary>
            /// Whether this asset object should act as a sensor fixture, which will cause the game
            /// to ignore all other physical properties of this object, and only react to collision events.
            /// </summary>
            /// <remarks>The default value in GameMaker for this is <see langword="false"/>.</remarks>
            public bool Sensor;

            /// <summary>
            /// The collision shape the asset object uses.
            /// </summary>
            /// <remarks>The default value in GameMaker Studio 1 for this is
            /// <see cref="Core.Models.GMObject.PhysicsProperties.CollisionShape.Circle"/> while in Studio 2 it is
            /// <see cref="Core.Models.GMObject.PhysicsProperties.CollisionShape.Box"/>.</remarks>
            public Core.Models.GMObject.PhysicsProperties.CollisionShape Shape;

            /// <summary>
            /// The physics density of the asset object.
            /// </summary>
            /// <remarks>Density is defined as mass per unit volume, with mass being automatically calculated by
            /// this density value and the unit volume being taken from the surface area of the shape. <br/>
            /// The default value in Gamemaker for this is <c>0.5</c>.</remarks>
            public float Density;

            /// <summary>
            /// Determines how "bouncy" a asset object is and is co-dependant on other attributes like <c>Gravity</c> or
            /// <see cref="Friction"/>.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <c>0.1</c>.</remarks>
            public float Restitution;

            /// <summary>
            /// The collision group this asset object belongs to.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <c>0</c>.</remarks>
            public int Group;

            /// <summary>
            /// The amount of linear damping this asset object has, which will gradually slow down moving objects.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <c>0.1</c></remarks>
            public float LinearDamping;

            /// <summary>
            /// The amount of angular damping this asset object has, which will slow down rotating objects.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <c>0.1</c>.</remarks>
            public float AngularDamping;

            /// <summary>
            /// The list of vertices used for
            /// <see cref="Core.Models.GMObject.PhysicsProperties.CollisionShape"/>.<see cref="Core.Models.GMObject.PhysicsProperties.CollisionShape.Custom"/>.
            /// </summary>
            public List<PhysicsVertex> Vertices;

            /// <summary>
            /// The amount of friction this asset object has, which will cause a loss of momentum during collisions.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <c>0.2</c>.</remarks>
            public float Friction;

            /// <summary>
            /// Whether the asset object should use physics simulation on object creation.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <see langword="true"/>.</remarks>
            public bool IsAwake;

            /// <summary>
            /// Whether the asset object should be kinematic, which makes it unaffected by collisions and other physics properties
            /// Will only be used if <see cref="Density"/> is set to <c>0</c>.
            /// </summary>
            /// <remarks>The default value for this in GameMaker is <see langword="false"/>.</remarks>
            public bool IsKinematic;

            /// <summary>
            /// An explicit cast from a <see cref="Core.Models.GMObject"/>.<see cref="Core.Models.GMObject.PhysicsProperties"/>
            /// struct to a <see cref="PhysicsProperties"/>.
            /// </summary>
            /// <param name="physicsProperties">The physics properties as
            /// <see cref="Core.Models.GMObject"/>.<see cref="Core.Models.GMObject.PhysicsProperties"/>.</param>
            /// <returns>Physics properties as <see cref="PhysicsProperties"/>.</returns>
            public static explicit operator PhysicsProperties(Core.Models.GMObject.PhysicsProperties physicsProperties)
            {
                PhysicsProperties newPhysics = new PhysicsProperties
                {
                    IsEnabled = physicsProperties.IsEnabled,
                    Sensor = physicsProperties.Sensor,
                    Shape = physicsProperties.Shape,
                    Density = physicsProperties.Density,
                    Restitution = physicsProperties.Restitution,
                    Group = physicsProperties.Group,
                    LinearDamping = physicsProperties.LinearDamping,
                    AngularDamping = physicsProperties.AngularDamping,
                    Friction = physicsProperties.Friction,
                    IsAwake = physicsProperties.IsAwake,
                    IsKinematic = physicsProperties.IsKinematic,
                };
                foreach (Core.Models.GMObject.PhysicsVertex v in physicsProperties.Vertices)
                    newPhysics.Vertices.Add(new PhysicsVertex { X = v.X, Y = v.Y });

                return newPhysics;
            }
        }

        public string Sprite { get; set; }
        public bool Visible { get; set; }
        public bool Managed { get; set; }
        public bool Solid { get; set; }
        public int Depth { get; set; }
        public bool Persistent { get; set; }
        public string ParentObject { get; set; }
        public string MaskSprite { get; set; }
        public PhysicsProperties Physics { get; set; }
        public SortedDictionary<EventType, List<Event>> Events { get; set; }

        public enum EventType : uint
        {
            Create = 0,
            Destroy = 1,
            Alarm = 2,
            Step = 3,
            Collision = 4,
            Keyboard = 5,
            Mouse = 6,
            Other = 7,
            Draw = 8,
            KeyPress = 9,
            KeyRelease = 10,
            Trigger = 11,
            CleanUp = 12,
            Gesture = 13,
            PreCreate = 14
        }

        public struct PhysicsVertex
        {
            public float X { get; set; }
            public float Y { get; set; }
        }

        public class EventConverter : JsonConverter<Event>
        {
            public override Event Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Utf8JsonReader lookAhead = reader;
                if (lookAhead.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();
                if (!lookAhead.Read() || lookAhead.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                Event baseEvent;
                switch (lookAhead.GetString())
                {
                    case "AlarmNumber":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventAlarm), options);
                        break;
                    case "SubtypeStep":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventStep), options);
                        break;
                    case "ObjectName":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventCollision), options);
                        break;
                    case "SubtypeKey":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventKeyboard), options);
                        break;
                    case "SubtypeMouse":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventMouse), options);
                        break;
                    case "SubtypeOther":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventOther), options);
                        break;
                    case "SubtypeDraw":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventDraw), options);
                        break;
                    case "SubtypeGesture":
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventGesture), options);
                        break;
                    default:
                        baseEvent = (Event)JsonSerializer.Deserialize(ref reader, typeof(EventNormal), options);
                        break;
                }
                return baseEvent;
            }

            public override void Write(Utf8JsonWriter writer, Event value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }

        [ProjectFile.JsonInterfaceConverter(typeof(EventConverter))]
        public interface Event
        {
            public List<Action> Actions { get; set; }
        }

        public struct EventNormal : Event
        {
            public List<Action> Actions { get; set; }
        }

        public struct EventAlarm : Event
        {
            public int AlarmNumber { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventStep : Event
        {
            public enum Subtype : uint
            {
                Normal = 0,
                Begin = 1,
                End = 2
            }
            public Subtype SubtypeStep { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventCollision : Event
        {
            public string ObjectName { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventKeyboard : Event
        {
            public enum Subtype : uint
            {
                vk_nokey = 0,
                vk_anykey = 1,
                vk_backspace = 8,
                vk_tab = 9,
                vk_return = 13,
                vk_enter = 13,
                vk_shift = 16,
                vk_control = 17,
                vk_alt = 18,
                vk_pause = 19,
                vk_escape = 27,
                vk_space = 32,
                vk_pageup = 33,
                vk_pagedown = 34,
                vk_end = 35,
                vk_home = 36,
                vk_left = 37,
                vk_up = 38,
                vk_right = 39,
                vk_down = 40,
                vk_printscreen = 44,
                vk_insert = 45,
                vk_delete = 46,
                Digit0 = 48,
                Digit1 = 49,
                Digit2 = 50,
                Digit3 = 51,
                Digit4 = 52,
                Digit5 = 53,
                Digit6 = 54,
                Digit7 = 55,
                Digit8 = 56,
                Digit9 = 57,
                A = 65,
                B = 66,
                C = 67,
                D = 68,
                E = 69,
                F = 70,
                G = 71,
                H = 72,
                I = 73,
                J = 74,
                K = 75,
                L = 76,
                M = 77,
                N = 78,
                O = 79,
                P = 80,
                Q = 81,
                R = 82,
                S = 83,
                T = 84,
                U = 85,
                V = 86,
                W = 87,
                X = 88,
                Y = 89,
                Z = 90,
                vk_numpad0 = 96,
                vk_numpad1 = 97,
                vk_numpad2 = 98,
                vk_numpad3 = 99,
                vk_numpad4 = 100,
                vk_numpad5 = 101,
                vk_numpad6 = 102,
                vk_numpad7 = 103,
                vk_numpad8 = 104,
                vk_numpad9 = 105,
                vk_multiply = 106,
                vk_add = 107,
                vk_subtract = 109,
                vk_decimal = 110,
                vk_divide = 111,
                vk_f1 = 112,
                vk_f2 = 113,
                vk_f3 = 114,
                vk_f4 = 115,
                vk_f5 = 116,
                vk_f6 = 117,
                vk_f7 = 118,
                vk_f8 = 119,
                vk_f9 = 120,
                vk_f10 = 121,
                vk_f11 = 122,
                vk_f12 = 123,
                vk_lshift = 160,
                vk_rshift = 161,
                vk_lcontrol = 162,
                vk_rcontrol = 163,
                vk_lalt = 164,
                vk_ralt = 165
            }
            public Subtype SubtypeKey { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventMouse : Event
        {
            public enum Subtype : uint
            {
                LeftButton = 0,
                RightButton = 1,
                MiddleButton = 2,
                NoButton = 3,
                LeftPressed = 4,
                RightPressed = 5,
                MiddlePressed = 6,
                LeftReleased = 7,
                RightReleased = 8,
                MiddleReleased = 9,
                MouseEnter = 10,
                MouseLeave = 11,
                Joystick1Left = 16,
                Joystick1Right = 17,
                Joystick1Up = 18,
                Joystick1Down = 19,
                Joystick1Button1 = 21,
                Joystick1Button2 = 22,
                Joystick1Button3 = 23,
                Joystick1Button4 = 24,
                Joystick1Button5 = 25,
                Joystick1Button6 = 26,
                Joystick1Button7 = 27,
                Joystick1Button8 = 28,
                Joystick2Left = 31,
                Joystick2Right = 32,
                Joystick2Up = 33,
                Joystick2Down = 34,
                Joystick2Button1 = 36,
                Joystick2Button2 = 37,
                Joystick2Button3 = 38,
                Joystick2Button4 = 39,
                Joystick2Button5 = 40,
                Joystick2Button6 = 41,
                Joystick2Button7 = 42,
                Joystick2Button8 = 43,
                GlobLeftButton = 50,
                GlobRightButton = 51,
                GlobMiddleButton = 52,
                GlobLeftPressed = 53,
                GlobRightPressed = 54,
                GlobMiddlePressed = 55,
                GlobLeftReleased = 56,
                GlobRightReleased = 57,
                GlobMiddleReleased = 58,
                MouseWheelUp = 60,
                MouseWheelDown = 61
            }
            public Subtype SubtypeMouse { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventOther : Event
        {
            public enum Subtype : uint
            {
                OutsideRoom = 0,
                IntersectBoundary = 1,
                GameStart = 2,
                GameEnd = 3,
                RoomStart = 4,
                RoomEnd = 5,
                NoMoreLives = 6,
                AnimationEnd = 7,
                EndOfPath = 8,
                NoMoreHealth = 9,
                User0 = 10,
                User1 = 11,
                User2 = 12,
                User3 = 13,
                User4 = 14,
                User5 = 15,
                User6 = 16,
                User7 = 17,
                User8 = 18,
                User9 = 19,
                User10 = 20,
                User11 = 21,
                User12 = 22,
                User13 = 23,
                User14 = 24,
                User15 = 25,
                User16 = 26,
                OutsideView0 = 40,
                OutsideView1 = 41,
                OutsideView2 = 42,
                OutsideView3 = 43,
                OutsideView4 = 44,
                OutsideView5 = 45,
                OutsideView6 = 46,
                OutsideView7 = 47,
                BoundaryView0 = 50,
                BoundaryView1 = 51,
                BoundaryView2 = 52,
                BoundaryView3 = 53,
                BoundaryView4 = 54,
                BoundaryView5 = 55,
                BoundaryView6 = 56,
                BoundaryView7 = 57,
                AnimationUpdate = 58,
                AnimationEvent = 59,
                AsyncImageLoaded = 60,
                AsyncSoundLoaded = 61,
                AsyncHTTP = 62,
                AsyncDialog = 63,
                AsyncIAP = 66,
                AsyncCloud = 67,
                AsyncNetworking = 68,
                AsyncSteam = 69,
                AsyncSocial = 70,
                AsyncPushNotification = 71,
                AsyncSaveAndLoad = 72,
                AsyncAudioRecording = 73,
                AsyncAudioPlayback = 74,
                AsyncSystem = 75
            }
            public Subtype SubtypeOther { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventDraw : Event
        {
            public enum Subtype : uint
            {
                Draw = 0,
                DrawGUI = 64,
                Resize = 65,
                DrawBegin = 72,
                DrawEnd = 73,
                DrawGUIBegin = 74,
                DrawGUIEnd = 75,
                PreDraw = 76,
                PostDraw = 77
            }
            public Subtype SubtypeDraw { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct EventGesture : Event
        {
            public enum Subtype : uint
            {
                Tap = 0,
                DoubleTap = 1,
                DragStart = 2,
                DragMove = 3,
                DragEnd = 4,
                Flick = 5,
                PinchStart = 6,
                PinchIn = 7,
                PinchOut = 8,
                PinchEnd = 9,
                RotateStart = 10,
                Rotating = 11,
                RotateEnd = 12,
                GlobalTap = 64,
                GlobalDoubleTap = 65,
                GlobalDragStart = 66,
                GlobalDragMove = 67,
                GlobalDragEnd = 68,
                GlobalFlick = 69,
                GlobalPinchStart = 70,
                GlobalPinchIn = 71,
                GlobalPinchOut = 72,
                GlobalPinchEnd = 73,
                GlobalRotateStart = 74,
                GlobalRotating = 75
            }
            public Subtype SubtypeGesture { get; set; }
            public List<Action> Actions { get; set; }
        }

        public struct Action
        {
            public string Code { get; set; }
            public int ID { get; set; }
            public bool UseApplyTo { get; set; }
            public string ActionName { get; set; }
            public int ArgumentCount { get; set; }
        }

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetObject>(buff, ProjectFile.JsonOptions);
            ComputeHash(res, buff);
            return res;
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, ProjectFile.JsonOptions);
            if (actuallyWrite)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);
            }
            return buff;
        }

        public override void Delete(string assetPath)
        {
            if (File.Exists(assetPath))
                File.Delete(assetPath);

            string dir = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }
    }
}
