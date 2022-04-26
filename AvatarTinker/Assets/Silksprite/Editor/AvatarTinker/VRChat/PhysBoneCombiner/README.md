# PhysBone合成ツール

## 概要

アバターのPhysBoneをまとめて。

## 前提条件

本ツールはVRChatのAvatar 3.0を前提とします。

本ツールは、同じ親GameObjectの下に、同一のパラメータが指定されたPhysBoneが複数刺さっている状況を想定しています。
以下の場合、本ツールを使っても何も嬉しいことはありません。

- PhysBoneコンポーネントのrootTransformの親が一致していない
- PhysBoneコンポーネントの揺れ方の設定がバラバラである
- Phys Bone Transform Countが多いわりに、Phys Bone Componentsがそこまで多くない

以下の状況は想定していません。
変換に失敗したり、データが失われたりするかもしれません。

- ConstraintやJointなど、ボーンを操作するコンポーネントを利用している場合
- PhysBoneコンポーネントへの参照が存在していた場合、変換時に全て失われます。

## 役に立つ状況

Phys Bone Componentsが多すぎてパフォーマンスランクを制約しているアバターを変換すると、Phys Bone Componentsが減るためパフォーマンスランクが改善します。
ただし、引き換えにPhys Bone Transform Countが増加するため、闇雲に変換してもパフォーマンスランクは改善しません。

また、あくまでパフォーマンスランクが改善するだけなので、負荷軽減の役には立ちません。

## 使い方

1. プロジェクトのバックアップを取ってください。
2. 「Window/Avatar Tinker/VRChat/Phys Bone Combiner」メニューからウィンドウを開いて、指示に従ってください。
