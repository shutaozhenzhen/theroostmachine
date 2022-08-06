using Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Commands;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Logic;
using SecretHistories.NullObjects;
using SecretHistories.Spheres;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Roost.World.Shelves
{
    [IsEncaustableClass(typeof(ShelfCreationCommand))]
    class ShelfPayload : ITokenPayload
    {
        // ITokenPayload mostly garbage stuff
        private Token _token;
        private List<AbstractDominion> _dominions = new List<AbstractDominion>();
        private List<PopulateDominionCommand> _storedDominionCommands = new List<PopulateDominionCommand>();
        [DontEncaust]
        public Token Token
        {
            get
            {
                {
                    if (_token == null)
                        return NullToken.Create();
                    return _token;
                }
            }
        }
        [Encaust]
        public List<AbstractDominion> Dominions
        {
            get { return new List<AbstractDominion>(_dominions); }
        }

        [Encaust]
        public string EntityId { get; set; }
        [DontEncaust] public string Label => "Zone";
        [DontEncaust] public string Description => "Description";
        [Encaust] public int Quantity => 1;
        [DontEncaust] public string UniquenessGroup => string.Empty;
        [DontEncaust] public bool Unique => false;
        [DontEncaust] public string Icon => string.Empty;
        private List<Sphere> _spheres { get; set; }
        [DontEncaust] public bool Metafictional => true;
        [DontEncaust] public Dictionary<string, int> Mutations { get; }
        [DontEncaust] public bool IsOpen => false;
        [Encaust] public string Id { get; private set; }
#pragma warning disable 67
        public event Action<TokenPayloadChangedArgs> OnChanged;
        public event Action<float> OnLifetimeSpent;
#pragma warning restore 67

        public ShelfPayload(string id, string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) //backward compatibility!
            {
                EntityId = entityId;
                Id = $"shelf_{entityId}";
            }
            else
            {
                Id = id;
                EntityId = entityId;
            }

            if (!Id.StartsWith(FucinePath.TOKEN.ToString()))
                Id = FucinePath.TOKEN + Id;
        }


        public bool ApplyExoticEffect(ExoticEffect exoticEffect) { return false; }

        public void AttachSphere(Sphere sphere)
        {
            // ...
        }

        public bool CanInteractWith(ITokenPayload incomingTokenPayload)
        {
            return false;
        }

        public bool CanMergeWith(ITokenPayload incomingTokenPayload)
        {
            return false;
        }

        public void Close()
        {
            // ...
        }

        public void Conclude()
        {
            // ...
        }

        public void DetachSphere(Sphere sphere)
        {
            // ...
        }

        public void ExecuteHeartbeat(float seconds, float metaseconds)
        {
            // ...
        }

        public void ExecuteTokenEffectCommand(IAffectsTokenCommand command)
        {
            // ...
        }

        public void FirstHeartbeat()
        {
            // ...
        }

        public FucinePath GetAbsolutePath()
        {
            var pathAbove = _token.Sphere.GetAbsolutePath();
            var absolutePath = pathAbove.AppendingToken(this.Id);
            return absolutePath;
        }

        public AspectsDictionary GetAspects(bool includeSelf)
        {
            return new AspectsDictionary();
        }

        public Sphere GetEnRouteSphere()
        {
            if (Token.Sphere.GoverningSphereSpec.EnRouteSpherePath.IsValid() && !Token.Sphere.GoverningSphereSpec.EnRouteSpherePath.IsEmpty())
                return Watchman.Get<HornedAxe>().GetSphereByPath(Token.Sphere, Token.Sphere.GoverningSphereSpec.EnRouteSpherePath);

            return Token.Sphere.GetContainer().GetEnRouteSphere();
        }

        public string GetIllumination(string key)
        {
            return string.Empty;
        }

        public Dictionary<string, string> GetIlluminations()
        {
            return new Dictionary<string, string>();
        }

        public Type GetManifestationType(Sphere sphere)
        {
            return typeof(ShelfManifestation);
        }

        public RectTransform GetRectTransform()
        {
            return Token.TokenRectTransform;
        }

        public string GetSignature()
        {
            return Id;
        }

        public Sphere GetSphereById(string id)
        {
            return _spheres.SingleOrDefault(s => s.Id == id && !s.Defunct);
        }

        public List<Sphere> GetSpheres()
        {
            return new List<Sphere>();
        }

        public List<Sphere> GetSpheresByCategory(SphereCategory category)
        {
            return new List<Sphere>(_spheres.Where(c => c.SphereCategory == category && !c.Defunct));
        }

        public Timeshadow GetTimeshadow()
        {
            return Timeshadow.CreateTimelessShadow();
        }

        public Token GetToken()
        {
            return Token;
        }

        public FucinePath GetWildPath()
        {
            var wildPath = FucinePath.Wild();
            return wildPath.AppendingToken(this.Id); ;
        }

        public Sphere GetWindowsSphere()
        {
            if (Token.Sphere.GoverningSphereSpec.WindowsSpherePath.IsValid() && !Token.Sphere.GoverningSphereSpec.WindowsSpherePath.IsEmpty())
                return Watchman.Get<HornedAxe>().GetSphereByPath(Token.Sphere, Token.Sphere.GoverningSphereSpec.WindowsSpherePath);

            return Token.Sphere.GetContainer().GetWindowsSphere();
        }

        public void InitialiseManifestation(IManifestation manifestation)
        {
            manifestation.Initialise(this);   
        }

        public void InteractWithIncoming(Token incomingToken)
        {
            // ...
        }

        public bool IsPermanent()
        {
            return false;
        }

        public bool IsValid()
        {
            return true;
        }

        public bool IsValidElementStack()
        {
            return false;
        }

        public bool ManifestationAcceptableForPayloadInSphere(IManifestation manifestation, Sphere sphere)
        {
            return !(manifestation.GetType() != GetManifestationType(sphere));
        }

        public void ModifyQuantity(int unsatisfiedChange, Context context)
        {
            // ...
        }

        public void OnTokenMoved(TokenLocation toLocation)
        {
            // ...
        }

        public void OpenAt(TokenLocation location)
        {
            // ...
        }

        public bool ReceiveNote(INotification notification, Context context)
        {
            return true;
        }

        public bool RegisterDominion(AbstractDominion dominionToRegister)
        {
            dominionToRegister.OnSphereAdded.AddListener(AttachSphere);
            dominionToRegister.OnSphereRemoved.AddListener(DetachSphere);

            if (_dominions.Contains(dominionToRegister))
                return false;

            _dominions.Add(dominionToRegister);


            foreach (var storedPopulateDominionCommand in _storedDominionCommands)
            {
                if (dominionToRegister.Identifier == storedPopulateDominionCommand.Identifier)
                    storedPopulateDominionCommand.Execute(dominionToRegister);
            }



            return true;
        }

        public bool Retire(RetirementVFX VFX)
        {
            return true;
        }

        public void SetIllumination(string key, string value)
        {
            // ...
        }

        public void SetMutation(string mutationEffectMutate, int mutationEffectLevel, bool mutationEffectAdditive)
        {
            // ...
        }

        public void SetQuantity(int quantityToLeaveBehind, Context context)
        {
            // ...
        }

        public void SetToken(Token token)
        {
            _token = token;
        }

        public void ShowNoMergeMessage(ITokenPayload incomingTokenPayload)
        {
            // ...
        }

        public void StorePopulateDominionCommand(PopulateDominionCommand populateDominionCommand)
        {
            // ...
        }
    }
}
