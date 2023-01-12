﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FF9;
using Memoria;
using Memoria.Data;
using Memoria.Database;
using Memoria.Scenes;
using UnityEngine;

public partial class BattleHUD : UIScene
{
    public override void Show(SceneVoidDelegate afterFinished = null)
    {
        SceneVoidDelegate action = GeneratedShow;
        if (afterFinished != null)
            action = (SceneVoidDelegate)Delegate.Combine(action, afterFinished);

        if (!_isFromPause)
        {
            base.Show(action);
            PersistenSingleton<UIManager>.Instance.Booster.SetBoosterState(PersistenSingleton<UIManager>.Instance.UnityScene);
            FF9StateSystem.Settings.SetMasterSkill();
            this._doubleCastCount = 0;
            AllMenuPanel.SetActive(false);
        }
        else
        {
            _commandEnable = _beforePauseCommandEnable;
            _isTryingToRun = false;
            Singleton<HUDMessage>.Instance.Pause(false);
            base.Show(action);
            if (_commandEnable && !_hidingHud)
            {
                FF9BMenu_EnableMenu(true);
                ButtonGroupState.ActiveGroup = _currentButtonGroup;
                DisplayTargetPointer();
            }
        }
        _isFromPause = false;
        _oneTime = true;
    }

    public override void Hide(SceneVoidDelegate afterFinished = null)
    {
        base.Hide(afterFinished);
        PauseButtonGameObject.SetActive(false);
        HelpButtonGameObject.SetActive(false);
        if (_isFromPause)
            return;

        RemoveCursorMemorize();
    }

    //public override GameObject OnKeyNavigate(KeyCode direction, GameObject currentObj, GameObject nextObj)
    //{
    //    if (nextObj != null || currentObj == null)
    //        return nextObj;
    //    if (!Configuration.Control.WrapSomeMenus)
    //        return null;
    //    if (ButtonGroupState.ActiveGroup == CommandGroupButton)
    //    {
    //        if ((CommandMenu)currentObj.transform.GetSiblingIndex() == CommandMenu.Item && direction == KeyCode.DownArrow)
    //        {
    //            return _commandPanel.GetCommandButton(CommandMenu.Attack);
    //            //OnItemSelect();
    //            //FF9Sfx.FF9SFX_Play(103);
    //        }
    //    }
    //    else if (ButtonGroupState.ActiveGroup == TargetGroupButton && _cursorType == CursorGroup.Individual)
    //    {
    //    }
    //    return null;
    //}

    public override Boolean OnKeyConfirm(GameObject go)
    {
        if (!base.OnKeyConfirm(go) || _hidingHud)
            return true;

        if (ButtonGroupState.ActiveGroup == CommandGroupButton)
        {
            FF9Sfx.FF9SFX_Play(103);
            CommandMenu menuType = _currentCommandIndex;
            _currentCommandIndex = (CommandMenu)go.transform.GetSiblingIndex();
            _currentCommandId = GetCommandFromCommandIndex(ref menuType, CurrentPlayerIndex);
            _commandCursorMemorize[CurrentPlayerIndex] = _currentCommandIndex;
            _subMenuType = SubMenuType.Normal;
            if (IsDoubleCast && _doubleCastCount < 2)
                ++_doubleCastCount;

            switch (menuType)
            {
                case CommandMenu.Attack:
                    SetCommandVisibility(false, false);
                    SetTargetVisibility(true);
                    break;
                case CommandMenu.Defend:
                    _targetCursor = 0;
                    SendCommand(ProcessCommand(CurrentPlayerIndex, CursorGroup.Individual));
                    SetIdle();
                    break;
                case CommandMenu.Ability1:
                case CommandMenu.Ability2:
                    CharacterCommand ff9Command = CharacterCommands.Commands[_currentCommandId];
                    if (ff9Command.Type == CharacterCommandType.Normal)
                    {
                        _subMenuType = SubMenuType.Normal;
                        SetCommandVisibility(false, false);
                        SetTargetVisibility(true);
                    }
                    else if (ff9Command.Type == CharacterCommandType.Ability)
                    {
                        _subMenuType = SubMenuType.Ability;
                        DisplayAbility();
                        SetCommandVisibility(false, false);
                        SetAbilityPanelVisibility(true, false);
                    }
                    else if (ff9Command.Type == CharacterCommandType.Throw)
                    {
                        _subMenuType = SubMenuType.Throw;
                        DisplayItem(true);
                        SetCommandVisibility(false, false);
                        SetItemPanelVisibility(true, false);
                    }
                    break;
                case CommandMenu.Item:
                    DisplayItem(false);
                    SetCommandVisibility(false, false);
                    SetItemPanelVisibility(true, false);
                    break;
                case CommandMenu.Change:
                    _targetCursor = 0;
                    if (_isManualTrance)
                    {
                        BattleUnit btl = FF9StateSystem.Battle.FF9Battle.GetUnit(CurrentPlayerIndex);
                        btl.Trance = Byte.MaxValue;
                        btl.AlterStatus(BattleStatus.Trance);
                    }
                    else
                    {
                        CommandDetail command = ProcessCommand(CurrentPlayerIndex, CursorGroup.Individual);
                        SendCommand(command);
                        SetIdle();
                    }
                    break;
            }
        }
        else if (ButtonGroupState.ActiveGroup == TargetGroupButton)
        {
            FF9Sfx.FF9SFX_Play(103);
            if (_cursorType == CursorGroup.Individual)
            {
                for (Int32 i = 0; i < _matchBattleIdEnemyList.Count; i++)
                {
                    if (i < _targetPanel.Enemies.Count && _targetPanel.Enemies[i].GameObject == go)
                    {
                        CheckDoubleCast(_matchBattleIdEnemyList[i], _cursorType);
                        return true;
                    }
                }

                for (Int32 i = 0; i < _matchBattleIdPlayerList.Count; i++)
                {
                    if (i < _targetPanel.Players.Count && _targetPanel.Players[i].GameObject == go)
                    {
                        CheckDoubleCast(_matchBattleIdPlayerList[i], _cursorType);
                        return true;
                    }
                }
            }
            else if (_cursorType == CursorGroup.AllPlayer || _cursorType == CursorGroup.AllEnemy || _cursorType == CursorGroup.All)
            {
                CheckDoubleCast(-1, _cursorType);
            }
        }
        else if (ButtonGroupState.ActiveGroup == AbilityGroupButton)
        {
            if (CheckAbilityStatus(go.GetComponent<RecycleListItem>().ItemDataIndex) == AbilityStatus.Enable)
            {
                FF9Sfx.FF9SFX_Play(103);
                _currentSubMenuIndex = go.GetComponent<RecycleListItem>().ItemDataIndex;
                _abilityCursorMemorize[new PairCharCommand(CurrentPlayerIndex, _currentCommandId)] = _currentSubMenuIndex;

                SetAbilityPanelVisibility(false, false);
                SetTargetVisibility(true);
            }
            else
            {
                FF9Sfx.FF9SFX_Play(102);
            }
        }
        else if (ButtonGroupState.ActiveGroup == ItemGroupButton)
        {
            if (_itemIdList[_currentSubMenuIndex] != RegularItem.NoItem)
            {
                FF9Sfx.FF9SFX_Play(103);
                _currentSubMenuIndex = go.GetComponent<RecycleListItem>().ItemDataIndex;
                _abilityCursorMemorize[new PairCharCommand(CurrentPlayerIndex, _currentCommandId)] = _currentSubMenuIndex;
                SetItemPanelVisibility(false, false);
                SetTargetVisibility(true);
            }
            else
            {
                FF9Sfx.FF9SFX_Play(102);
            }
        }
        return true;
    }

    public override Boolean OnKeyCancel(GameObject go)
    {
        if (UIManager.Input.GetKey(Control.Special))
            return true;

        if (base.OnKeyCancel(go) && !_hidingHud && ButtonGroupState.ActiveGroup != CommandGroupButton)
        {
            if (ButtonGroupState.ActiveGroup == TargetGroupButton)
            {
                CommandMenu menuType = _currentCommandIndex;
                GetCommandFromCommandIndex(ref menuType, CurrentPlayerIndex);
                FF9Sfx.FF9SFX_Play(101);
                SetTargetVisibility(false);
                ClearModelPointer();
                switch (menuType)
                {
                    case CommandMenu.Attack:
                        SetCommandVisibility(true, true);
                        break;
                    case CommandMenu.Ability1:
                    case CommandMenu.Ability2:
                        if (_subMenuType == SubMenuType.Ability)
                        {
                            SetAbilityPanelVisibility(true, true);
                            break;
                        }
                        if (_subMenuType == SubMenuType.Throw)
                        {
                            SetItemPanelVisibility(true, true);
                            break;
                        }
                        SetCommandVisibility(true, true);
                        break;
                    case CommandMenu.Item:
                        SetItemPanelVisibility(true, true);
                        break;
                }
            }
            else if (ButtonGroupState.ActiveGroup == AbilityGroupButton)
            {
                FF9Sfx.FF9SFX_Play(101);
                if (IsDoubleCast && _doubleCastCount > 0)
                    --_doubleCastCount;
                if (_doubleCastCount == 0)
                {
                    SetAbilityPanelVisibility(false, false);
                    SetCommandVisibility(true, true);
                }
                else
                {
                    SetAbilityPanelVisibility(true, false);
                }
            }
            else if (ButtonGroupState.ActiveGroup == ItemGroupButton)
            {
                FF9Sfx.FF9SFX_Play(101);
                SetItemPanelVisibility(false, false);
                SetCommandVisibility(true, true);
            }
            else if (ButtonGroupState.ActiveGroup == String.Empty && UIManager.Input.ContainsAndroidQuitKey())
            {
                OnKeyQuit();
            }
        }
        return true;
    }

    public override Boolean OnKeyMenu(GameObject go)
    {
        if (base.OnKeyMenu(go) && !_hidingHud && ButtonGroupState.ActiveGroup == CommandGroupButton)
        {
            if (ReadyQueue.Count > 1)
            {
                Int32 postponed = ReadyQueue[0];
                ReadyQueue.RemoveAt(0);
                ReadyQueue.Add(postponed);
                using (List<Int32>.Enumerator enumerator = ReadyQueue.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        Int32 current = enumerator.Current;
                        if (!InputFinishList.Contains(current) && !_unconsciousStateList.Contains(current) && current != CurrentPlayerIndex)
                        {
                            if (ReadyQueue.IndexOf(current) > 0)
                            {
                                ReadyQueue.Remove(current);
                                ReadyQueue.Insert(0, current);
                            }
                            SwitchPlayer(current);
                            break;
                        }
                    }
                }
            }
            else if (ReadyQueue.Count == 1)
            {
                SwitchPlayer(ReadyQueue[0]);
            }
        }
        return true;
    }

    public override Boolean OnKeyPause(GameObject go)
    {
        if (base.OnKeyPause(go) && FF9StateSystem.Battle.FF9Battle.btl_seq != 2 && FF9StateSystem.Battle.FF9Battle.btl_seq != 1)
        {
            NextSceneIsModal = true;
            _isFromPause = true;
            _beforePauseCommandEnable = _commandEnable;
            _currentButtonGroup = !_hidingHud ? ButtonGroupState.ActiveGroup : _currentButtonGroup;
            FF9BMenu_EnableMenu(false);
            Singleton<HUDMessage>.Instance.Pause(true);
            Hide(() => PersistenSingleton<UIManager>.Instance.ChangeUIState(UIManager.UIState.Pause));
        }
        return base.OnKeyPause(go);
    }

    public override void OnKeyQuit()
    {
        if (Loading || FF9StateSystem.Battle.FF9Battle.btl_seq == 2 || FF9StateSystem.Battle.FF9Battle.btl_seq == 1)
            return;
        _beforePauseCommandEnable = _commandEnable;
        _currentButtonGroup = ButtonGroupState.ActiveGroup;
        FF9BMenu_EnableMenu(false);
        ShowQuitUI(_onResumeFromQuit);
    }

    public override Boolean OnKeyLeftBumper(GameObject go)
    {
        if (base.OnKeyLeftBumper(go) && !_hidingHud && ButtonGroupState.ActiveGroup == TargetGroupButton && (_targetCursor == TargetType.ManyAny || _targetCursor == TargetType.ManyEnemy || _targetCursor == TargetType.ManyAlly))
        {
            FF9Sfx.FF9SFX_Play(103);
            _isAllTarget = !_isAllTarget;
            _allTargetToggle.value = _isAllTarget;
            _allTargetButtonComponent.SetState(UIButtonColor.State.Normal, false);
            ToggleAllTarget();
        }
        return true;
    }

    public override Boolean OnKeyRightBumper(GameObject go)
    {
        if (base.OnKeyRightBumper(go) && !_hidingHud && ButtonGroupState.ActiveGroup == TargetGroupButton && (_targetCursor == TargetType.ManyAny || _targetCursor == TargetType.ManyEnemy || _targetCursor == TargetType.ManyAlly))
        {
            FF9Sfx.FF9SFX_Play(103);
            _isAllTarget = !_isAllTarget;
            _allTargetToggle.value = _isAllTarget;
            _allTargetButtonComponent.SetState(UIButtonColor.State.Normal, false);
            ToggleAllTarget();
        }
        return true;
    }

    public override Boolean OnKeyRightTrigger(GameObject go)
    {
        if (base.OnKeyRightTrigger(go) && !_hidingHud && !AndroidTvOnKeyRightTrigger(go))
            ProcessAutoBattleInput();
        return true;
    }

    public override Boolean OnItemSelect(GameObject go)
    {
        if (base.OnItemSelect(go))
        {
            if (ButtonGroupState.ActiveGroup == CommandGroupButton)
                _currentCommandIndex = (CommandMenu)go.transform.GetSiblingIndex();
            else if (ButtonGroupState.ActiveGroup == AbilityGroupButton || ButtonGroupState.ActiveGroup == ItemGroupButton)
                _currentSubMenuIndex = go.GetComponent<RecycleListItem>().ItemDataIndex;
            if (ButtonGroupState.ActiveGroup == TargetGroupButton && _cursorType == CursorGroup.Individual)
            {
                if (go.transform.parent == modelButtonManager.transform)
                {
                    Int32 targetIndex = go.GetComponent<ModelButton>().index;
                    Int32 targetLabelIndex = targetIndex >= HonoluluBattleMain.EnemyStartIndex ? _matchBattleIdEnemyList.IndexOf(targetIndex) : _matchBattleIdPlayerList.IndexOf(targetIndex);
                    if (targetLabelIndex != -1)
                    {
                        if (targetIndex >= HonoluluBattleMain.EnemyStartIndex)
                            targetLabelIndex += HonoluluBattleMain.EnemyStartIndex;
                        GONavigationButton targetHud = _targetPanel.AllTargets[targetLabelIndex];
                        if (targetHud.ButtonGroup.enabled)
                            ButtonGroupState.ActiveButton = targetHud.GameObject;
                    }
                }
                else if (go.transform.parent.parent == TargetPanel.transform)
                {
                    Int32 targetIndex = go.transform.GetSiblingIndex();
                    if (go.GetParent().transform.GetSiblingIndex() == 1)
                        targetIndex += HonoluluBattleMain.EnemyStartIndex;
                    if (_currentTargetIndex != targetIndex)
                    {
                        _currentTargetIndex = targetIndex;
                        DisplayTargetPointer();
                    }
                }
            }
        }
        return true;
    }

    private void RemoveCursorMemorize()
    {
        _commandCursorMemorize.Clear();
        _abilityCursorMemorize.Clear();

        ButtonGroupState.RemoveCursorMemorize(CommandGroupButton);
        ButtonGroupState.RemoveCursorMemorize(ItemGroupButton);
        ButtonGroupState.RemoveCursorMemorize(AbilityGroupButton);
        ButtonGroupState.RemoveCursorMemorize(TargetGroupButton);
    }

    private Boolean AndroidTvOnKeyRightTrigger(GameObject go)
    {
        return CheckAndroidTVModule(Control.RightTrigger);
    }

    private AbilityStatus CheckAbilityStatus(Int32 subMenuIndex)
    {
        if (CommandIsMonsterTransformCommand(CurrentPlayerIndex, _currentCommandId, out _))
            return AbilityStatus.Enable;
        CharacterCommand command = CharacterCommands.Commands[_currentCommandId];
        BattleAbilityId abilId = command.GetAbilityId(subMenuIndex);
        if (abilId == BattleAbilityId.Void)
            return AbilityStatus.None;
        return GetAbilityState(ff9abil.GetAbilityIdFromActiveAbility(abilId));
    }

    private void ToggleAllTarget()
    {
        if (_cursorType == CursorGroup.AllEnemy || _cursorType == CursorGroup.AllPlayer)
        {
            if (ButtonGroupState.ActiveButton)
            {
                ButtonGroupState.SetButtonAnimation(ButtonGroupState.ActiveButton, true);
            }
            else
            {
                foreach (GONavigationButton button in _targetPanel.AllTargets)
                    ButtonGroupState.SetButtonAnimation(button, true);
                
                ButtonGroupState.ActiveButton = ButtonGroupState.GetCursorStartSelect(TargetGroupButton);
            }
            _cursorType = CursorGroup.Individual;
            _targetPanel.ActivateButtons(false);
        }
        else
        {
            ButtonGroupState.SetButtonAnimation(ButtonGroupState.ActiveButton, false);
            Singleton<PointerManager>.Instance.RemovePointerFromGameObject(ButtonGroupState.ActiveButton);
            _cursorType = _currentTargetIndex >= HonoluluBattleMain.EnemyStartIndex ? CursorGroup.AllEnemy : CursorGroup.AllPlayer;
            _targetPanel.ActivateButtons(true);
        }
        SetTargetHelp();
        DisplayTargetPointer();
    }

    [CompilerGenerated]
    private void GeneratedShow()
    {
        PersistenSingleton<UIManager>.Instance.SetPlayerControlEnable(true, null);
        PersistenSingleton<UIManager>.Instance.SetGameCameraEnable(true);
        PersistenSingleton<UIManager>.Instance.SetMenuControlEnable(true);
        PersistenSingleton<UIManager>.Instance.SetUIPauseEnable(true);
        PauseButtonGameObject.SetActive(PersistenSingleton<UIManager>.Instance.IsPauseControlEnable && FF9StateSystem.MobilePlatform);
        HelpButtonGameObject.SetActive(PersistenSingleton<UIManager>.Instance.IsPauseControlEnable && FF9StateSystem.MobilePlatform);
        ButtonGroupState.SetScrollButtonToGroup(_abilityScrollList.ScrollButton, AbilityGroupButton);
        ButtonGroupState.SetScrollButtonToGroup(_itemScrollList.ScrollButton, ItemGroupButton);
        ButtonGroupState.SetPointerOffsetToGroup(new Vector2(34f, 0.0f), AbilityGroupButton);
        ButtonGroupState.SetPointerOffsetToGroup(new Vector2(34f, 0.0f), ItemGroupButton);
        ButtonGroupState.SetPointerOffsetToGroup(new Vector2(16f, 0.0f), TargetGroupButton);
        ButtonGroupState.SetPointerOffsetToGroup(new Vector2(10f, 0.0f), CommandGroupButton);
        ButtonGroupState.SetPointerLimitRectToGroup(AbilityPanel.GetComponent<UIWidget>(), _abilityScrollList.cellHeight, AbilityGroupButton);
        ButtonGroupState.SetPointerLimitRectToGroup(ItemPanel.GetComponent<UIWidget>(), _itemScrollList.cellHeight, ItemGroupButton);
    }
}