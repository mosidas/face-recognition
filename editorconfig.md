# editoconfig説明

## 基本設定（全ファイル）

| 設定項目                 | 値    | 説明                      |
| ------------------------ | ----- | ------------------------- |
| indent_style             | space | スペースでインデント      |
| indent_size              | 2     | インデント幅2スペース     |
| end_of_line              | lf    | 改行コードLF（Unix形式）  |
| charset                  | utf-8 | 文字エンコーディングUTF-8 |
| trim_trailing_whitespace | true  | 行末の空白を削除          |
| insert_final_newline     | true  | ファイル末尾に改行を挿入  |

  ---
## C#ファイル設定
- using ディレクティブ（12-15行目）

| 設定項目                                | 値    | 重要度 | 説明                             |
| --------------------------------------- | ----- | ------ | -------------------------------- |
| dotnet_separate_import_directive_groups | false | -      | using 文をグループ分けしない     |
| dotnet_sort_system_directives_first     | true  | -      | System 名前空間を最初に配置      |
| file_header_template                    | unset | -      | ファイルヘッダーテンプレートなし |

- this. の使用（17-21行目）

| 設定項目                                | 値    | 重要度     | 説明                          |
| --------------------------------------- | ----- | ---------- | ----------------------------- |
| dotnet_style_qualification_for_event    | false | ⚠️ warning | イベントで this. を使わない   |
| dotnet_style_qualification_for_field    | false | ⚠️ warning | フィールドで this. を使わない |
| dotnet_style_qualification_for_method   | false | ⚠️ warning | メソッドで this. を使わない   |
| dotnet_style_qualification_for_property | false | ⚠️ warning | プロパティで this. を使わない |

- 型の指定（23-25行目）

| 設定項目                                                   | 値   | 重要度     | 説明                         |
| ---------------------------------------------------------- | ---- | ---------- | ---------------------------- |
| dotnet_style_predefined_type_for_locals_parameters_members | true | ⚠️ warning | int を使う（Int32 ではなく） |
| dotnet_style_predefined_type_for_member_access             | true | ⚠️ warning | int.MaxValue を使う          |

- 括弧の使用（27-31行目）

| 設定項目                                                | 値                   | 重要度 | 説明                 |
| ------------------------------------------------------- | -------------------- | ------ | -------------------- |
| dotnet_style_parentheses_in_arithmetic_binary_operators | always_for_clarity   | silent | 算術演算で括弧を使う |
| dotnet_style_parentheses_in_other_binary_operators      | always_for_clarity   | silent | 論理演算で括弧を使う |
| dotnet_style_parentheses_in_other_operators             | never_if_unnecessary | silent | 不要な括弧は使わない |
| dotnet_style_parentheses_in_relational_binary_operators | always_for_clarity   | silent | 関係演算で括弧を使う |

- モディファイア（33-34行目）

| 設定項目                                     | 値     | 重要度     | 説明                     |
| -------------------------------------------- | ------ | ---------- | ------------------------ |
| dotnet_style_require_accessibility_modifiers | always | ⚠️ warning | アクセス修飾子を常に明示 |

- 式レベルの設定（36-54行目）

| 設定項目                                                         | 値                       | 重要度     | 説明                        |
| ---------------------------------------------------------------- | ------------------------ | ---------- | --------------------------- |
| dotnet_style_coalesce_expression                                 | true                     | ⚠️ warning | ?? 演算子を使う             |
| dotnet_style_collection_initializer                              | true                     | ⚠️ warning | コレクション初期化子を使う  |
| dotnet_style_explicit_tuple_names                                | true                     | ⚠️ warning | タプル名を明示的に使う      |
| dotnet_style_namespace_match_folder                              | true                     | ⚠️ warning | 名前空間=フォルダ構造       |
| dotnet_style_null_propagation                                    | true                     | ⚠️ warning | ?. 演算子を使う             |
| dotnet_style_object_initializer                                  | true                     | ⚠️ warning | オブジェクト初期化子を使う  |
| dotnet_style_operator_placement_when_wrapping                    | beginning_of_line        | -          | 演算子を行頭に配置          |
| dotnet_style_prefer_auto_properties                              | true                     | ⚠️ warning | 自動プロパティを使う        |
| dotnet_style_prefer_collection_expression                        | when_types_loosely_match | ⚠️ warning | コレクション式 [...] を使う |
| dotnet_style_prefer_compound_assignment                          | true                     | ⚠️ warning | += などを使う               |
| dotnet_style_prefer_conditional_expression_over_assignment       | true                     | silent     | 代入に三項演算子            |
| dotnet_style_prefer_conditional_expression_over_return           | true                     | silent     | return に三項演算子         |
| dotnet_style_prefer_foreach_explicit_cast_in_source              | when_strongly_typed      | ⚠️ warning | foreach で明示的キャスト    |
| dotnet_style_prefer_inferred_anonymous_type_member_names         | true                     | ⚠️ warning | 匿名型で名前推論            |
| dotnet_style_prefer_inferred_tuple_names                         | true                     | ⚠️ warning | タプルで名前推論            |
| dotnet_style_prefer_is_null_check_over_reference_equality_method | true                     | ⚠️ warning | is null を使う              |
| dotnet_style_prefer_simplified_boolean_expressions               | true                     | ⚠️ warning | 論理式を簡略化              |
| dotnet_style_prefer_simplified_interpolation                     | true                     | ⚠️ warning | 文字列補間を簡略化          |

- フィールド設定（56-57行目）

| 設定項目                    | 値   | 重要度     | 説明                              |
| --------------------------- | ---- | ---------- | --------------------------------- |
| dotnet_style_readonly_field | true | ⚠️ warning | 変更されないフィールドに readonly |

- パラメータ設定（59-60行目）

| 設定項目                              | 値  | 重要度      | 説明                   |
| ------------------------------------- | --- | ----------- | ---------------------- |
| dotnet_code_quality_unused_parameters | all | 💡 suggestion | 未使用パラメータを検出 |

- 警告抑制（62-63行目）

| 設定項目                                         | 値   | 説明                 |
| ------------------------------------------------ | ---- | -------------------- |
| dotnet_remove_unnecessary_suppression_exclusions | none | 不要な警告抑制を検出 |

- var の使用（65-68行目）

| 設定項目                               | 値    | 重要度     | 説明                          |
| -------------------------------------- | ----- | ---------- | ----------------------------- |
| csharp_style_var_elsewhere             | false | silent     | その他で var を使わない       |
| csharp_style_var_for_built_in_types    | false | silent     | 組み込み型で var を使わない   |
| csharp_style_var_when_type_is_apparent | false | ⚠️ warning | 型が明らかでも var を使わない |

- 式形式メンバー（70-78行目）

| 設定項目                                       | 値    | 重要度      | 説明                           |
| ---------------------------------------------- | ----- | ----------- | ------------------------------ |
| csharp_style_expression_bodied_accessors       | true  | silent      | アクセサーで => を使う         |
| csharp_style_expression_bodied_constructors    | false | silent      | コンストラクタで => を使わない |
| csharp_style_expression_bodied_indexers        | true  | silent      | インデクサーで => を使う       |
| csharp_style_expression_bodied_lambdas         | true  | 💡 suggestion | ラムダで => を使う             |
| csharp_style_expression_bodied_local_functions | false | silent      | ローカル関数で => を使わない   |
| csharp_style_expression_bodied_methods         | false | silent      | メソッドで => を使わない       |
| csharp_style_expression_bodied_operators       | false | silent      | 演算子で => を使わない         |
| csharp_style_expression_bodied_properties      | true  | silent      | プロパティで => を使う         |

- パターンマッチング（80-86行目）

| 設定項目                                              | 値   | 重要度      | 説明                                         |
| ----------------------------------------------------- | ---- | ----------- | -------------------------------------------- |
| csharp_style_pattern_matching_over_as_with_null_check | true | ⚠️ warning  | as + null チェックではなくパターンマッチング |
| csharp_style_pattern_matching_over_is_with_cast_check | true | ⚠️ warning  | is + キャストではなくパターンマッチング      |
| csharp_style_prefer_extended_property_pattern         | true | 💡 suggestion | 拡張プロパティパターンを使う                 |
| csharp_style_prefer_not_pattern                       | true | 💡 suggestion | not パターンを使う                           |
| csharp_style_prefer_pattern_matching                  | true | silent      | パターンマッチングを優先                     |
| csharp_style_prefer_switch_expression                 | true | 💡 suggestion | switch 式を使う                              |

- null チェック（88-89行目）

| 設定項目                               | 値   | 重要度      | 説明                            |
| -------------------------------------- | ---- | ----------- | ------------------------------- |
| csharp_style_conditional_delegate_call | true | 💡 suggestion | デリゲート呼び出しに ?.Invoke() |

- モディファイア（91-96行目）

| 設定項目                                   | 値                 | 重要度      | 説明                    |
| ------------------------------------------ | ------------------ | ----------- | ----------------------- |
| csharp_prefer_static_anonymous_function    | true               | ⚠️ warning  | 静的ラムダを使う        |
| csharp_prefer_static_local_function        | true               | ⚠️ warning  | 静的ローカル関数を使う  |
| csharp_preferred_modifier_order            | public,private,... | 💡 suggestion | モディファイアの順序    |
| csharp_style_prefer_readonly_struct        | true               | 💡 suggestion | readonly struct を使う  |
| csharp_style_prefer_readonly_struct_member | true               | ⚠️ warning  | readonly メンバーを使う |

- コードブロック（98-104行目）

| 設定項目                                    | 値          | 重要度     | 説明                           |
| ------------------------------------------- | ----------- | ---------- | ------------------------------ |
| csharp_prefer_braces                        | true        | ⚠️ warning | 常に波括弧を使う               |
| csharp_prefer_simple_using_statement        | true        | ⚠️ warning | using var を使う               |
| csharp_style_namespace_declarations         | file_scoped | ⚠️ warning | ファイルスコープ名前空間       |
| csharp_style_prefer_method_group_conversion | true        | silent     | メソッドグループ変換           |
| csharp_style_prefer_primary_constructors    | true        | ⚠️ warning | プライマリコンストラクタを使う |
| csharp_style_prefer_top_level_statements    | true        | silent     | トップレベルステートメント     |

- 式レベル設定（106-119行目）

| 設定項目                                                    | 値               | 重要度     | 説明                 |
| ----------------------------------------------------------- | ---------------- | ---------- | -------------------- |
| csharp_prefer_simple_default_expression                     | true             | ⚠️ warning | default を使う       |
| csharp_style_deconstructed_variable_declaration             | true             | ⚠️ warning | 分解宣言を使う       |
| csharp_style_implicit_object_creation_when_type_is_apparent | true             | ⚠️ warning | new() を使う         |
| csharp_style_inlined_variable_declaration                   | true             | ⚠️ warning | out var を使う       |
| csharp_style_prefer_index_operator                          | true             | silent     | ^ インデックス演算子 |
| csharp_style_prefer_local_over_anonymous_function           | true             | ⚠️ warning | ローカル関数を優先   |
| csharp_style_prefer_null_check_over_type_check              | true             | ⚠️ warning | is null を使う       |
| csharp_style_prefer_range_operator                          | true             | ⚠️ warning | .. 範囲演算子を使う  |
| csharp_style_prefer_tuple_swap                              | true             | ⚠️ warning | タプルスワップを使う |
| csharp_style_prefer_utf8_string_literals                    | true             | ⚠️ warning | UTF-8 文字列リテラル |
| csharp_style_throw_expression                               | true             | ⚠️ warning | throw 式を使う       |
| csharp_style_unused_value_assignment_preference             | discard_variable | ⚠️ warning | 未使用値を _ に      |
| csharp_style_unused_value_expression_statement_preference   | discard_variable | ⚠️ warning | 未使用式を _ に      |

- using ディレクティブ（121-122行目）

| 設定項目                         | 値                | 重要度     | 説明                     |
| -------------------------------- | ----------------- | ---------- | ------------------------ |
| csharp_using_directive_placement | outside_namespace | ⚠️ warning | using を名前空間の外側に |


## フォーマット規則

- 改行設定（126-133行目）

| 設定項目                                              | 値   | 説明                            |
| ----------------------------------------------------- | ---- | ------------------------------- |
| csharp_new_line_before_catch                          | true | catch の前に改行                |
| csharp_new_line_before_else                           | true | else の前に改行                 |
| csharp_new_line_before_finally                        | true | finally の前に改行              |
| csharp_new_line_before_members_in_anonymous_types     | true | 匿名型メンバーを改行            |
| csharp_new_line_before_members_in_object_initializers | true | 初期化子メンバーを改行          |
| csharp_new_line_before_open_brace                     | all  | すべての { の前に改行（Allman） |
| csharp_new_line_between_query_expression_clauses      | true | LINQ 句を改行                   |

- インデント設定（135-141行目）

| 設定項目                               | 値                    | 説明                      |
| -------------------------------------- | --------------------- | ------------------------- |
| csharp_indent_block_contents           | true                  | ブロック内容をインデント  |
| csharp_indent_braces                   | false                 | 波括弧をインデントしない  |
| csharp_indent_case_contents            | true                  | case 内容をインデント     |
| csharp_indent_case_contents_when_block | true                  | case ブロックをインデント |
| csharp_indent_labels                   | one_less_than_current | ラベルは1レベル少なく     |
| csharp_indent_switch_labels            | true                  | case ラベルをインデント   |

- スペース設定（143-165行目）

| 設定項目                                                                 | 値               | 説明                                    |
| ------------------------------------------------------------------------ | ---------------- | --------------------------------------- |
| csharp_space_after_cast                                                  | false            | キャスト後にスペースなし                |
| csharp_space_after_colon_in_inheritance_clause                           | true             | 継承の : 後にスペース                   |
| csharp_space_after_comma                                                 | true             | , 後にスペース                          |
| csharp_space_after_dot                                                   | false            | . 後にスペースなし                      |
| csharp_space_after_keywords_in_control_flow_statements                   | true             | if, for 後にスペース                    |
| csharp_space_after_semicolon_in_for_statement                            | true             | for の ; 後にスペース                   |
| csharp_space_around_binary_operators                                     | before_and_after | 二項演算子の前後にスペース              |
| csharp_space_around_declaration_statements                               | false            | 宣言文の周りにスペースなし              |
| csharp_space_before_colon_in_inheritance_clause                          | true             | 継承の : 前にスペース                   |
| csharp_space_before_comma                                                | false            | , 前にスペースなし                      |
| csharp_space_before_dot                                                  | false            | . 前にスペースなし                      |
| csharp_space_before_open_square_brackets                                 | false            | [ 前にスペースなし                      |
| csharp_space_before_semicolon_in_for_statement                           | false            | for の ; 前にスペースなし               |
| csharp_space_between_empty_square_brackets                               | false            | [] 内にスペースなし                     |
| csharp_space_between_method_call_empty_parameter_list_parentheses        | false            | () 内にスペースなし                     |
| csharp_space_between_method_call_name_and_opening_parenthesis            | false            | メソッド名と ( の間にスペースなし       |
| csharp_space_between_method_call_parameter_list_parentheses              | false            | パラメータリストの括弧内にスペースなし  |
| csharp_space_between_method_declaration_empty_parameter_list_parentheses | false            | 宣言の () 内にスペースなし              |
| csharp_space_between_method_declaration_name_and_open_parenthesis        | false            | 宣言のメソッド名と ( の間にスペースなし |
| csharp_space_between_method_declaration_parameter_list_parentheses       | false            | 宣言のパラメータリスト内にスペースなし  |
| csharp_space_between_parentheses                                         | false            | 括弧内にスペースなし                    |
| csharp_space_between_square_brackets                                     | false            | [] 内にスペースなし                     |

- 折り返し設定（167-169行目）

| 設定項目                               | 値   | 説明                    |
| -------------------------------------- | ---- | ----------------------- |
| csharp_preserve_single_line_blocks     | true | 1行ブロックを保持       |
| csharp_preserve_single_line_statements | true | 1行ステートメントを保持 |


## 命名ルール

| 対象                       | 命名規則    | 重要度     | 例                             |
| -------------------------- | ----------- | ---------- | ------------------------------ |
| 型・名前空間               | PascalCase  | ⚠️ warning | UnifiedDetector                |
| インターフェース           | IPascalCase | ⚠️ warning | IFaceRecognizer                |
| 型パラメータ               | TPascalCase | ⚠️ warning | TValue                         |
| メソッド                   | PascalCase  | ⚠️ warning | ProcessFrame                   |
| プロパティ                 | PascalCase  | ⚠️ warning | Name                           |
| イベント                   | PascalCase  | ⚠️ warning | DataReceived                   |
| ローカル変数               | camelCase   | ⚠️ warning | totalCount                     |
| ローカル定数               | camelCase   | ⚠️ warning | maxRetries                     |
| パラメータ                 | camelCase   | ⚠️ warning | itemCount                      |
| パブリックフィールド       | PascalCase  | ⚠️ warning | MaxValue                       |
| プライベートフィールド     | _camelCase  | ⚠️ warning | _detector                      |
| プライベート静的フィールド | s_camelCase | silent     | s_instance                     |
| パブリック定数             | PascalCase  | ⚠️ warning | MaxRetries                     |
| プライベート定数           | PascalCase  | ⚠️ warning | DefaultTimeout                 |
| パブリック静的readonly     | PascalCase  | ⚠️ warning | Empty                          |
| プライベート静的readonly   | PascalCase  | ⚠️ warning | DefaultValue                   |
| 列挙型                     | PascalCase  | ⚠️ warning | CameraSourceType               |
| ローカル関数               | PascalCase  | ⚠️ warning | CalculateTotal                 |
| 非フィールドメンバー       | PascalCase  | ⚠️ warning | プロパティ、イベント、メソッド |


## 重要度レベルの凡例

| レベル     | 説明                 | `dotnet format` |
| ---------- | -------------------- | --------------- |
| error      | エラー（ビルド失敗） | 修正される      |
| warning    | 警告（黄色い波線）   | 修正される      |
| suggestion | 提案（電球アイコン） | 修正されない    |
| silent     | 警告なし             | 修正されない    |
