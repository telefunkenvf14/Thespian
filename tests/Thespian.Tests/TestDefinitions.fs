﻿module Nessos.Thespian.Tests.TestDefinitions

open System
open Nessos.Thespian

type TestMessage<'T, 'R> =
  | TestAsync of 'T
  | TestSync of IReplyChannel<'R> * 'T

type TestMessage<'T> = TestMessage<'T, unit>


module PrimitiveBehaviors =
  let nill (self: Actor<TestMessage<unit>>) = async.Zero()
  let consumeOne (self: Actor<TestMessage<'T>>) =
    async {
      let! m = self.Receive()
      match m with
      | TestAsync _ -> ()
      | TestSync(R reply, _) -> reply <| Value()
    }
  let selfStop (self: Actor<TestMessage<unit>>) =
    async {
      let! m = self.Receive()
      match m with
      | TestSync(R reply, _) -> reply <| Value(); self.Stop()
      | _ -> self.Stop()
    }
