# リセットアニメーション自動生成くん

## 概要

表情アニメーションをWrite Defaultsオフで運用する際に必要になる、アニメーションクリップに含まれていないプロパティを初期状態に戻すためのリセットアニメーションを生成します。
このツールはシーンに置いてある状態を初期状態として利用し、Animator Controllerから参照されている全てのプロパティを初期状態に戻します。

## 前提条件

本ツールはVRChatのAvatar 3.0を前提とします。

以下の場合、変換できません。

- ボーン構造が一致していない場合
- ボーンの位置やサイズが調整されている場合

表情アニメーションをWrite Defaultsオフで実装している必要があります。
また、リセットアニメーションはAnimator Controllerの一番上のレイヤーに常時再生で設定されることを想定しています。

## 役に立つ状況

Write Defaultsオンの表情アニメーションを使いまわしている人、あるいはWrite Defaultsオフで表情アニメーションを自作している人はおおむね役に立つと思います。

## 使い方

1. プロジェクトのバックアップを取ってください。
2. 「Window/Avatar Tinker/VRChat/Reset Clip Generator」メニューからウィンドウを開いて、指示に従ってください。