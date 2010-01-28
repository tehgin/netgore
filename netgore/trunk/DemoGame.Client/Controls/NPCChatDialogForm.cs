using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NetGore;
using NetGore.Graphics.GUI;
using NetGore.NPCChat;

namespace DemoGame.Client
{
    delegate void ChatDialogSelectResponseHandler(NPCChatDialogForm sender, NPCChatResponseBase response);

    delegate void ChatDialogRequestEndDialogHandler(NPCChatDialogForm sender);

    class NPCChatDialogForm : Form
    {
        static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        const int _numDisplayedResponses = 4;

        readonly TextBox _dialogTextControl;
        readonly ResponseText[] _responseTextControls = new ResponseText[_numDisplayedResponses];
        NPCChatDialogBase _dialog;
        NPCChatDialogItemBase _page;

        byte _responseOffset = 0;

        NPCChatResponseBase[] _responses;

        /// <summary>
        /// Notifies listeners when this NPCChatDialogForm wants to end the chat dialog.
        /// </summary>
        public event ChatDialogRequestEndDialogHandler OnRequestEndDialog;

        /// <summary>
        /// Notifies listeners when a dialog response was chosen.
        /// </summary>
        public event ChatDialogSelectResponseHandler OnSelectResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="NPCChatDialogForm"/> class.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="parent">The parent.</param>
        public NPCChatDialogForm(Vector2 position, Control parent) : base(parent, position, new Vector2(400, 300))
        {
            IsVisible = false;

            // NOTE: We want to use a scrollable textbox here... when we finally make one

            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            float spacing = Font.LineSpacing;
            // ReSharper restore DoNotCallOverridableMethodsInConstructor

            float responseStartY = ClientSize.Y - (_numDisplayedResponses * spacing);
            Vector2 textboxSize = ClientSize - new Vector2(0, ClientSize.Y - responseStartY);
            _dialogTextControl = new TextBox(this, Vector2.Zero, textboxSize)
            { IsEnabled = false, CanFocus = false, IsMultiLine = true };
            _dialogTextControl.ClientSize -= _dialogTextControl.Border.Size;

            for (byte i = 0; i < _numDisplayedResponses; i++)
            {
                ResponseText r = new ResponseText(this, new Vector2(5, responseStartY + (spacing * i))) { IsVisible = true };
                r.OnClick += ResponseText_OnClick;
                _responseTextControls[i] = r;
            }
        }

        /// <summary>
        /// Gets if a dialog is currently open.
        /// </summary>
        public bool IsChatting
        {
            get { return _dialog != null; }
        }

        public void EndDialog()
        {
            IsVisible = false;
            _dialog = null;
        }

        /// <summary>
        /// Handles when a key is being pressed while the <see cref="Control"/> has focus.
        /// This is called immediately before <see cref="Control.OnKeyPress"/>.
        /// Override this method instead of using an event hook on <see cref="Control.OnKeyPress"/> when possible.
        /// </summary>
        /// <param name="e">The event args.</param>
        protected override void KeyPress(KeyboardEventArgs e)
        {
            base.KeyPress(e);

            if (e.Keys.Contains(Keys.Escape))
            {
                if (OnRequestEndDialog != null)
                    OnRequestEndDialog(this);
            }
            else
            {
                var asNumeric = e.Keys.Select(x => x.GetNumericKeyAsValue()).FirstOrDefault(x => x.HasValue);
                if (asNumeric.HasValue)
                {
                    var value = asNumeric.Value - 1;
                    if (value < 0)
                        value = 10;

                    if (value < _responses.Length)
                    {
                        if (OnSelectResponse != null)
                            OnSelectResponse(this, _responses[value]);
                    }
                }
            }
        }

        void ResponseText_OnClick(object sender, MouseClickEventArgs e)
        {
            ResponseText src = (ResponseText)sender;
            NPCChatResponseBase response = src.Response;
            if (response != null)
            {
                if (OnSelectResponse != null)
                    OnSelectResponse(this, response);
            }
        }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();

            Text = "NPC Chat";
        }

        public void SetPageIndex(ushort pageIndex, IEnumerable<byte> responsesToSkip)
        {
            _page = _dialog.GetDialogItem(pageIndex);

            if (_page == null)
            {
                const string errmsg =
                    "Page `{0}` in dialog `{1}` is null. The Client should never be trying to set an invalid page.";
                if (log.IsErrorEnabled)
                    log.ErrorFormat(errmsg, pageIndex, _dialog);
                Debug.Fail(string.Format(errmsg, pageIndex, _dialog));
                EndDialog();
                return;
            }

            if (responsesToSkip != null)
                _responses = _page.Responses.Where(x => !responsesToSkip.Contains(x.Value)).OrderBy(x => x.Value).ToArray();
            else
                _responses = _page.Responses.OrderBy(x => x.Value).ToArray();

            _dialogTextControl.Text = _page.Text;
            SetResponseOffset(0);
            IsVisible = true;
        }

        public void SetResponseOffset(int newOffset)
        {
            if (newOffset > _responses.Length - _numDisplayedResponses)
                newOffset = _responses.Length - _numDisplayedResponses;
            if (newOffset < 0)
                newOffset = 0;

            Debug.Assert(_responseOffset >= 0);

            _responseOffset = (byte)newOffset;

            for (int i = 0; i < _numDisplayedResponses; i++)
            {
                int responseIndex = _responseOffset + i;

                if (responseIndex >= 0 && responseIndex < _responses.Length)
                    _responseTextControls[i].SetResponse(_responses[responseIndex], responseIndex);
                else
                    _responseTextControls[i].UnsetResponse();
            }
        }

        public void StartDialog(NPCChatDialogBase dialog)
        {
            if (dialog == null)
            {
                Debug.Fail("Dialog is null.");
                return;
            }

            _dialog = dialog;
            SetFocus();
        }

        class ResponseText : Label
        {
            NPCChatResponseBase _response;

            public ResponseText(Control parent, Vector2 position) : base(parent, position)
            {
            }

            public NPCChatResponseBase Response
            {
                get { return _response; }
            }

            public void SetResponse(NPCChatResponseBase response, int index)
            {
                if (response == null)
                {
                    Debug.Fail("Parameter 'response' should never be null. Use UnsetResponse() instead.");
                    UnsetResponse();
                    return;
                }

                _response = response;
                Text = (index + 1) + ": " + Response.Text;
            }

            public void UnsetResponse()
            {
                _response = null;
                Text = string.Empty;
            }
        }
    }
}