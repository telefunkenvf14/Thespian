﻿namespace Nessos.Thespian.Remote
    
open System

open Nessos.Thespian
open Nessos.Thespian.Utils.Concurrency
open Nessos.Thespian.Remote
open Nessos.Thespian.Remote.TcpProtocol

[<AutoOpen>]
module Constants =
    let DefaultTcpPort = 2753
    
module Uri =

    let private (|UTCP|_|) (protocolName: string) = if protocolName = Protocols.UTCP then Some() else None
    let private (|BTCP|_|) (protocolName: string) = if protocolName = Protocols.BTCP then Some() else None
    let private (|NPP|_|) (protocolName: string) = if protocolName = Protocols.NPP then Some() else None
    let private (|TCP|_|) (protocolName: string) =
        match protocolName with
        | UTCP | BTCP  -> Some()
        | _ -> None

    type IUriParser =
        abstract Parse: Uri -> ActorRef<'T>

    type TcpParser internal () =
        interface IUriParser with
            override __.Parse (uri: Uri): ActorRef<'T> =
                let port = if uri.Port = -1 then DefaultTcpPort else uri.Port
                let address = new Address(uri.Host, port)

                let factory = match uri.Scheme with
                              | UTCP -> new Unidirectional.UTcpFactory(Unidirectional.Client address) :> IProtocolFactory
                              | BTCP -> new Bidirectional.BTcpFactory(Unidirectional.Client address) :> IProtocolFactory
                              | _ -> failwith "Used tcp uri parser for non-tcp protocol."

                let actorName = uri.PathAndQuery.Substring(1)

                let protocol = factory.CreateClientInstance<'T>(actorName)

                new ActorRef<'T>(actorName, [| protocol |])

    type NppParser internal () =
        interface IUriParser with
            override __.Parse (uri: Uri): ActorRef<'T> =
                let processId = uri.Port
                let actorName = uri.PathAndQuery.Substring(1)
                let factory = new Remote.PipeProtocol.PipeProtocolFactory(processId) :> IProtocolFactory

                let protocol = factory.CreateClientInstance<'T>(actorName)

                new ActorRef<'T>(actorName, [| protocol |])
          

    let private initParsers() =
        let tcpParser = new TcpParser() :> IUriParser
        let nppParser = new NppParser() :> IUriParser
        Map.empty |> Map.add UTCP tcpParser
                  |> Map.add BTCP tcpParser
                  |> Map.add NPP nppParser
    
    type Config private() =
        static let parsers = Atom.create <| initParsers()
        static member TryGetParser(protocol: string) = parsers.Value.TryFind protocol

module ActorRef =
    open System
    open Nessos.Thespian

    /// <summary>
    ///     Exports URIs for ActorRef
    /// </summary>
    /// <param name="actorRef">ActorRef to be read.</param>
    let toUris (actorRef: ActorRef<'T>): string list = actorRef.GetUris()

    /// <summary>
    ///     Exports default URI for provided ActorRef.
    /// </summary>
    /// <param name="actorRef">ActorRef to be read.</param>
    let toUri (actorRef: ActorRef<'T>): string =
        try actorRef.GetUris() |> List.head
        with :? ArgumentException as e -> raise <| new ArgumentException("ActorRef not supporting URIs, perhaps due to an unpublished actor.", "actorRef", e)

    /// <summary>
    ///     Try creating an ActorRef instance from provided uri
    /// </summary>
    /// <param name="uri">Uri to actor.</param>
    let tryFromUri<'T> (uri: string): ActorRef<'T> option =
        let u = new System.Uri(uri, UriKind.Absolute)
        match Uri.Config.TryGetParser u.Scheme with
        | Some parser -> parser.Parse u |> Some
        | None -> None

    /// <summary>
    ///     Creates an ActorRef instance from provided uri
    /// </summary>
    /// <param name="uri">Uri to actor.</param>
    let fromUri<'T> (uri: string): ActorRef<'T> =
        match tryFromUri uri with
        | Some actorRef -> actorRef
        | None -> invalidArg "uri" "Unknown protocol uri."
