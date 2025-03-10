using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using SlackAPI.RPCMessages;
using System.IO;
namespace SlackAPI
{
    /// <summary>
    /// SlackClient is intended to solely handle RPC (HTTP-based) functionality. Does not handle WebSocket connectivity.
    ///
    /// For WebSocket connectivity, refer to <see cref="SlackAPI.SlackSocketClient"/>
    /// </summary>
    public class SlackClient : SlackClientBase
    {
        private readonly string APIToken;

        public Self MySelf;
        public User MyData;
        public Team MyTeam;

        public List<string> starredChannels;

        public List<User> Users;
        public List<Bot> Bots;
        public List<Channel> Channels;
        public List<Channel> Groups;
        public List<DirectMessageConversation> DirectMessages;

        public Dictionary<string, User> UserLookup;
        public Dictionary<string, Channel> ChannelLookup;
        public Dictionary<string, Channel> GroupLookup;
        public Dictionary<string, DirectMessageConversation> DirectMessageLookup;
        public Dictionary<string, Conversation> ConversationLookup;

        public SlackClient(string token)
        {
            APIToken = token;
        }

        public SlackClient(string token, IWebProxy proxySettings)
            : base(proxySettings)
        {
            APIToken = token;
        }

        public virtual void Connect(Action<LoginResponse> onConnected = null, Action onSocketConnected = null)
        {
            EmitLogin((loginDetails) =>
            {
                if (loginDetails.ok)
                    Connected(loginDetails);

                if (onConnected != null)
                    onConnected(loginDetails);
            });
        }

        protected virtual void Connected(LoginResponse loginDetails)
        {
            MySelf = loginDetails.self;
            MyTeam = loginDetails.team;

            UserLookup = new Dictionary<string, User>();
            ChannelLookup = new Dictionary<string, Channel>();
            ConversationLookup = new Dictionary<string, Conversation>();
            GroupLookup = new Dictionary<string, Channel>();
            DirectMessageLookup = new Dictionary<string, DirectMessageConversation>();
        }

        public void APIRequestWithToken<K>(Action<K> callback, params Tuple<string, string>[] getParameters)
            where K : Response
        {
            APIRequest(callback, getParameters, [], APIToken);
        }

        public void TestAuth(Action<AuthTestResponse> callback)
        {
            APIRequestWithToken(callback);
        }

        public void GetUserList(Action<UserListResponse> callback)
        {
            APIRequestWithToken(callback);
        }

        public void GetUserByEmail(Action<UserEmailLookupResponse> callback, string email)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("email", email));
        }

        public void ChannelsCreate(Action<ChannelCreateResponse> callback, string name)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("name", name));
        }

        public void ChannelsInvite(Action<ChannelInviteResponse> callback, string userId, string channelId)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("user", userId));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GetConversationsList(Action<ConversationsListResponse> callback, string cursor = "", bool ExcludeArchived = true, int limit = 100, string[] types = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>()
            {
                Tuple.Create("exclude_archived", ExcludeArchived ? "1" : "0")
            };
            if (limit > 0)
                parameters.Add(Tuple.Create("limit", limit.ToString()));
            if ((types != null) && types.Any())
                parameters.Add(Tuple.Create("types", string.Join(",", types)));
            if (!string.IsNullOrEmpty(cursor))
                parameters.Add(Tuple.Create("cursor", cursor));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GetConversationsMembers(Action<ConversationsMembersResponse> callback, string channelId, string cursor = "", int limit = 100)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>
            {
                new Tuple<string, string>("channel", channelId)
            };
            if (limit > 0)
                parameters.Add(Tuple.Create("limit", limit.ToString()));
            if (!string.IsNullOrEmpty(cursor))
                parameters.Add(Tuple.Create("cursor", cursor));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GetChannelList(Action<ChannelListResponse> callback, bool ExcludeArchived = true)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("exclude_archived", ExcludeArchived ? "1" : "0"), new Tuple<string, string>("limit", "1000"));
        }

        public void GetGroupsList(Action<GroupListResponse> callback, bool ExcludeArchived = true)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("exclude_archived", ExcludeArchived ? "1" : "0"));
        }

        public void GetDirectMessageList(Action<DirectMessageConversationListResponse> callback)
        {
            APIRequestWithToken(callback);
        }

        public void GetFiles(Action<FileListResponse> callback, string userId = null, DateTime? from = null, DateTime? to = null, int? count = null, int? page = null, FileTypes types = FileTypes.all, string channel = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            if (!string.IsNullOrEmpty(userId))
                parameters.Add(new Tuple<string, string>("user", userId));

            if (from.HasValue)
                parameters.Add(new Tuple<string, string>("ts_from", from.Value.ToProperTimeStamp()));

            if (to.HasValue)
                parameters.Add(new Tuple<string, string>("ts_to", to.Value.ToProperTimeStamp()));

            if (!types.HasFlag(FileTypes.all))
            {
                FileTypes[] values = (FileTypes[])Enum.GetValues(typeof(FileTypes));

                StringBuilder building = new StringBuilder();
                bool first = true;
                for (int i = 0; i < values.Length; ++i)
                {
                    if (types.HasFlag(values[i]))
                    {
                        if (!first) building.Append(",");

                        building.Append(values[i].ToString());

                        first = false;
                    }
                }

                if (building.Length > 0)
                    parameters.Add(new Tuple<string, string>("types", building.ToString()));
            }

            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));

            if (page.HasValue)
                parameters.Add(new Tuple<string, string>("page", page.Value.ToString()));

            if (!string.IsNullOrEmpty(channel))
                parameters.Add(new Tuple<string, string>("channel", channel));

            APIRequestWithToken(callback, [.. parameters]);
        }

        void GetHistory<K>(Action<K> historyCallback, string channel, DateTime? latest = null, DateTime? oldest = null, int? count = null, bool? unreads = false)
            where K : MessageHistory
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();
            parameters.Add(new Tuple<string, string>("channel", channel));

            if (latest.HasValue)
                parameters.Add(new Tuple<string, string>("latest", latest.Value.ToProperTimeStamp()));
            if (oldest.HasValue)
                parameters.Add(new Tuple<string, string>("oldest", oldest.Value.ToProperTimeStamp()));
            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));
            if (unreads.HasValue)
                parameters.Add(new Tuple<string, string>("unreads", unreads.Value ? "1" : "0"));

            APIRequestWithToken(historyCallback, [.. parameters]);
        }

        public void GetChannelHistory(Action<ChannelMessageHistory> callback, Channel channelInfo, DateTime? latest = null, DateTime? oldest = null, int? count = null, bool? unreads = false)
        {
            GetHistory(callback, channelInfo.id, latest, oldest, count, unreads);
        }

        public void GetDirectMessageHistory(Action<MessageHistory> callback, DirectMessageConversation conversationInfo, DateTime? latest = null, DateTime? oldest = null, int? count = null, bool? unreads = false)
        {
            GetHistory(callback, conversationInfo.id, latest, oldest, count, unreads);
        }

        public void GetGroupHistory(Action<GroupMessageHistory> callback, Channel groupInfo, DateTime? latest = null, DateTime? oldest = null, int? count = null, bool? unreads = false)
        {
            GetHistory(callback, groupInfo.id, latest, oldest, count, unreads);
        }

        public void GetConversationsHistory(Action<ConversationsMessageHistory> callback, Channel conversationInfo, DateTime? latest = null, DateTime? oldest = null, int? count = null, bool? unreads = false)
        {
            GetHistory(callback, conversationInfo.id, latest, oldest, count, unreads);
        }

        public void MarkChannel(Action<MarkResponse> callback, string channelId, DateTime ts)
        {
            APIRequestWithToken(callback,
                new Tuple<string, string>("channel", channelId),
                new Tuple<string, string>("ts", ts.ToProperTimeStamp())
            );
        }

        public void GetFileInfo(Action<FileInfoResponse> callback, string fileId, int? page = null, int? count = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("file", fileId));

            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));

            if (page.HasValue)
                parameters.Add(new Tuple<string, string>("page", page.Value.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }
        #region Groups
        public void GroupsArchive(Action<GroupArchiveResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void GroupsClose(Action<GroupCloseResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void GroupsCreate(Action<GroupCreateResponse> callback, string name)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("name", name));
        }

        public void GroupsCreateChild(Action<GroupCreateChildResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void GroupsInvite(Action<GroupInviteResponse> callback, string userId, string channelId)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("user", userId));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GroupsKick(Action<GroupKickResponse> callback, string userId, string channelId)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("user", userId));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GroupsLeave(Action<GroupLeaveResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void GroupsMark(Action<GroupMarkResponse> callback, string channelId, DateTime ts)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId), new Tuple<string, string>("ts", ts.ToProperTimeStamp()));
        }

        public void GroupsOpen(Action<GroupOpenResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void GroupsRename(Action<GroupRenameResponse> callback, string channelId, string name)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("name", name));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GroupsSetPurpose(Action<GroupSetPurposeResponse> callback, string channelId, string purpose)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("purpose", purpose));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GroupsSetTopic(Action<GroupSetPurposeResponse> callback, string channelId, string topic)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("topic", topic));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GroupsUnarchive(Action<GroupUnarchiveResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        #endregion

        #region Conversations
        public void ConversationsArchive(Action<ConversationsArchiveResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void ConversationsClose(Action<ConversationsCloseResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void ConversationsCreate(Action<ConversationsCreateResponse> callback, string name)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("name", name));
        }

        public void ConversationsInvite(Action<ConversationsInviteResponse> callback, string channelId, string[] userIds)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("users", string.Join(",", userIds)));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void ConversationsJoin(Action<ConversationsJoinResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void ConversationsKick(Action<ConversationsKickResponse> callback, string channelId, string userId)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("user", userId));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void ConversationsLeave(Action<ConversationsLeaveResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void ConversationsMark(Action<ConversationsMarkResponse> callback, string channelId, DateTime ts)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("ts", ts.ToProperTimeStamp()));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void ConversationsOpen(Action<ConversationsOpenResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        public void ConversationsRename(Action<ConversationsRenameResponse> callback, string channelId, string name)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("name", name));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void ConversationsSetPurpose(Action<ConversationsSetPurposeResponse> callback, string channelId, string purpose)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("purpose", purpose));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void ConversationsSetTopic(Action<ConversationsSetPurposeResponse> callback, string channelId, string topic)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("topic", topic));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void ConversationsUnarchive(Action<ConversationsUnarchiveResponse> callback, string channelId)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("channel", channelId));
        }

        #endregion


        public void SearchAll(Action<SearchResponseAll> callback, string query, string sorting = null, SearchSortDirection? direction = null, bool enableHighlights = false, int? count = null, int? page = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();
            parameters.Add(new Tuple<string, string>("query", query));

            if (sorting != null)
                parameters.Add(new Tuple<string, string>("sort", sorting));

            if (direction.HasValue)
                parameters.Add(new Tuple<string, string>("sort_dir", direction.Value.ToString()));

            if (enableHighlights)
                parameters.Add(new Tuple<string, string>("highlight", "1"));

            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));

            if (page.HasValue)
                parameters.Add(new Tuple<string, string>("page", page.Value.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void SearchMessages(Action<SearchResponseMessages> callback, string query, string sorting = null, SearchSortDirection? direction = null, bool enableHighlights = false, int? count = null, int? page = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();
            parameters.Add(new Tuple<string, string>("query", query));

            if (sorting != null)
                parameters.Add(new Tuple<string, string>("sort", sorting));

            if (direction.HasValue)
                parameters.Add(new Tuple<string, string>("sort_dir", direction.Value.ToString()));

            if (enableHighlights)
                parameters.Add(new Tuple<string, string>("highlight", "1"));

            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));

            if (page.HasValue)
                parameters.Add(new Tuple<string, string>("page", page.Value.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void SearchFiles(Action<SearchResponseFiles> callback, string query, string sorting = null, SearchSortDirection? direction = null, bool enableHighlights = false, int? count = null, int? page = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();
            parameters.Add(new Tuple<string, string>("query", query));

            if (sorting != null)
                parameters.Add(new Tuple<string, string>("sort", sorting));

            if (direction.HasValue)
                parameters.Add(new Tuple<string, string>("sort_dir", direction.Value.ToString()));

            if (enableHighlights)
                parameters.Add(new Tuple<string, string>("highlight", "1"));

            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));

            if (page.HasValue)
                parameters.Add(new Tuple<string, string>("page", page.Value.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GetStars(Action<StarListResponse> callback, string userId = null, int? count = null, int? page = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            if (!string.IsNullOrEmpty(userId))
                parameters.Add(new Tuple<string, string>("user", userId));

            if (count.HasValue)
                parameters.Add(new Tuple<string, string>("count", count.Value.ToString()));

            if (page.HasValue)
                parameters.Add(new Tuple<string, string>("page", page.Value.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void DeleteMessage(Action<DeletedResponse> callback, string channelId, DateTime ts)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>()
            {
                new Tuple<string,string>("ts", ts.ToProperTimeStamp()),
                new Tuple<string,string>("channel", channelId)
            };

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void EmitPresence(Action<PresenceResponse> callback, Presence status)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("presence", status.ToString()));
        }

        public void GetPreferences(Action<UserPreferencesResponse> callback)
        {
            APIRequestWithToken(callback);
        }

        #region Users

        public void GetCounts(Action<UserCountsResponse> callback)
        {
            APIRequestWithToken(callback);
        }

        public void GetPresence(Action<UserGetPresenceResponse> callback, string user)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("user", user));
        }

        public void GetInfo(Action<UserInfoResponse> callback, string user)
        {
            APIRequestWithToken(callback, new Tuple<string, string>("user", user));
        }

        #endregion

        public void EmitLogin(Action<LoginResponse> callback, string agent = "Inumedia.SlackAPI")
        {
            APIRequestWithToken(callback, new Tuple<string, string>("agent", agent));
        }

        public void Update(
            Action<UpdateResponse> callback,
            string ts,
            string channelId,
            string text,
            string botName = null,
            string parse = null,
            bool linkNames = false,
            IBlock[] blocks = null,
            Attachment[] attachments = null,
            bool? as_user = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("ts", ts));
            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("text", text));

            if (!string.IsNullOrEmpty(botName))
                parameters.Add(new Tuple<string, string>("username", botName));

            if (!string.IsNullOrEmpty(parse))
                parameters.Add(new Tuple<string, string>("parse", parse));

            if (linkNames)
                parameters.Add(new Tuple<string, string>("link_names", "1"));

            if (blocks != null && blocks.Length > 0)
                parameters.Add(new Tuple<string, string>("blocks",
                   JsonConvert.SerializeObject(blocks, new JsonSerializerSettings()
                   {
                       NullValueHandling = NullValueHandling.Ignore
                   })));

            if (attachments != null && attachments.Length > 0)
                parameters.Add(new Tuple<string, string>("attachments",
                   JsonConvert.SerializeObject(attachments, new JsonSerializerSettings()
                   {
                       NullValueHandling = NullValueHandling.Ignore
                   })));

            if (as_user.HasValue)
                parameters.Add(new Tuple<string, string>("as_user", as_user.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void JoinDirectMessageChannel(Action<JoinDirectMessageChannelResponse> callback, string user)
        {
            var param = new Tuple<string, string>("users", user);
            APIRequestWithToken(callback, param);
        }

        public void PostMessage(
            Action<PostMessageResponse> callback,
            string channelId,
            string text,
            string botName = null,
            string parse = null,
            bool linkNames = false,
            IBlock[] blocks = null,
            Attachment[] attachments = null,
            bool? unfurl_links = null,
            string icon_url = null,
            string icon_emoji = null,
            bool? as_user = null,
              string thread_ts = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>
            {
                new Tuple<string, string>("channel", channelId),
                new Tuple<string, string>("text", text)
            };

            if (!string.IsNullOrEmpty(botName))
                parameters.Add(new Tuple<string, string>("username", botName));

            if (!string.IsNullOrEmpty(parse))
                parameters.Add(new Tuple<string, string>("parse", parse));

            if (linkNames)
                parameters.Add(new Tuple<string, string>("link_names", "1"));

            if (blocks != null && blocks.Length > 0)
                parameters.Add(new Tuple<string, string>("blocks",
                   JsonConvert.SerializeObject(blocks, Formatting.None,
                      new JsonSerializerSettings // Shouldn't include a not set property
                      {
                          NullValueHandling = NullValueHandling.Ignore
                      })));

            if (attachments != null && attachments.Length > 0)
                parameters.Add(new Tuple<string, string>("attachments",
                    JsonConvert.SerializeObject(attachments, Formatting.None,
                            new JsonSerializerSettings // Shouldn't include a not set property
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            })));

            if (unfurl_links.HasValue)
                parameters.Add(new Tuple<string, string>("unfurl_links", unfurl_links.Value ? "true" : "false"));

            if (!string.IsNullOrEmpty(icon_url))
                parameters.Add(new Tuple<string, string>("icon_url", icon_url));

            if (!string.IsNullOrEmpty(icon_emoji))
                parameters.Add(new Tuple<string, string>("icon_emoji", icon_emoji));

            if (as_user.HasValue)
                parameters.Add(new Tuple<string, string>("as_user", as_user.ToString()));

            if (!string.IsNullOrEmpty(thread_ts))
                parameters.Add(new Tuple<string, string>("thread_ts", thread_ts));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void PostEphemeralMessage(
            Action<PostEphemeralResponse> callback,
            string channelId,
            string text,
            string targetuser,
            string parse = null,
            bool linkNames = false,
            Block[] blocks = null,
            Attachment[] attachments = null,
            bool as_user = false,
        string thread_ts = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("text", text));
            parameters.Add(new Tuple<string, string>("user", targetuser));

            if (!string.IsNullOrEmpty(parse))
                parameters.Add(new Tuple<string, string>("parse", parse));

            if (linkNames)
                parameters.Add(new Tuple<string, string>("link_names", "1"));

            if (blocks != null && blocks.Length > 0)
                parameters.Add(new Tuple<string, string>("blocks",
                    JsonConvert.SerializeObject(blocks, Formatting.None,
                            new JsonSerializerSettings // Shouldn't include a not set property
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            })));

            if (attachments != null && attachments.Length > 0)
                parameters.Add(new Tuple<string, string>("attachments",
                    JsonConvert.SerializeObject(attachments, Formatting.None,
                            new JsonSerializerSettings // Shouldn't include a not set property
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            })));

            parameters.Add(new Tuple<string, string>("as_user", as_user.ToString()));

            APIRequestWithToken(callback, [.. parameters]);
        }


        public void ScheduleMessage(
            Action<ScheduleMessageResponse> callback,
            string channelId,
            string text,
            DateTime post_at,
            string botName = null,
            string parse = null,
            bool linkNames = false,
            IBlock[] blocks = null,
            Attachment[] attachments = null,
            bool? unfurl_links = null,
            string icon_url = null,
            string icon_emoji = null,
            bool? as_user = null,
              string thread_ts = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("channel", channelId));
            parameters.Add(new Tuple<string, string>("text", text));
            parameters.Add(new Tuple<string, string>("post_at", Convert.ToUInt64((post_at - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString()));

            if (!string.IsNullOrEmpty(botName))
                parameters.Add(new Tuple<string, string>("username", botName));

            if (!string.IsNullOrEmpty(parse))
                parameters.Add(new Tuple<string, string>("parse", parse));

            if (linkNames)
                parameters.Add(new Tuple<string, string>("link_names", "1"));

            if (blocks != null && blocks.Length > 0)
                parameters.Add(new Tuple<string, string>("blocks",
                   JsonConvert.SerializeObject(blocks, Formatting.None,
                      new JsonSerializerSettings // Shouldn't include a not set property
                      {
                          NullValueHandling = NullValueHandling.Ignore
                      })));

            if (attachments != null && attachments.Length > 0)
                parameters.Add(new Tuple<string, string>("attachments",
                    JsonConvert.SerializeObject(attachments, Formatting.None,
                            new JsonSerializerSettings // Shouldn't include a not set property
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            })));

            if (unfurl_links.HasValue)
                parameters.Add(new Tuple<string, string>("unfurl_links", unfurl_links.Value ? "true" : "false"));

            if (!string.IsNullOrEmpty(icon_url))
                parameters.Add(new Tuple<string, string>("icon_url", icon_url));

            if (!string.IsNullOrEmpty(icon_emoji))
                parameters.Add(new Tuple<string, string>("icon_emoji", icon_emoji));

            if (as_user.HasValue)
                parameters.Add(new Tuple<string, string>("as_user", as_user.ToString()));

            if (!string.IsNullOrEmpty(thread_ts))
                parameters.Add(new Tuple<string, string>("thread_ts", thread_ts));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void DialogOpen(
           Action<DialogOpenResponse> callback,
           string triggerId,
           Dialog dialog)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            parameters.Add(new Tuple<string, string>("trigger_id", triggerId));

            parameters.Add(new Tuple<string, string>("dialog",
               JsonConvert.SerializeObject(dialog,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore
                  })));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void AddReaction(
            Action<ReactionAddedResponse> callback,
            string name = null,
            string channel = null,
            string timestamp = null)
        {
            List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

            if (!string.IsNullOrEmpty(name))
                parameters.Add(new Tuple<string, string>("name", name));

            if (!string.IsNullOrEmpty(channel))
                parameters.Add(new Tuple<string, string>("channel", channel));

            if (!string.IsNullOrEmpty(timestamp))
                parameters.Add(new Tuple<string, string>("timestamp", timestamp));

            APIRequestWithToken(callback, [.. parameters]);
        }

        public void GetUploadURLExternal(Action<GetUploadUrlExternalResponse> callback, string fileName, byte[] fileData)
        {
            List<Tuple<string, string>> parameters =
            [
                new Tuple<string, string>("filename", fileName),
                new Tuple<string, string>("length", fileData.Length.ToString()),
            ];
            APIRequestWithToken(callback, [.. parameters]);
        }

        public void CompleteUploadExternal(Action<CompleteUploadExternalResponse> callback, string fileId, string title, string[] channelIds, string initialComment = null, string thread_ts = null)
        {
            Uri target = new(Path.Combine(APIBaseLocation, "files.completeUploadExternal"));
            var files = new List<Dictionary<string, object>>{
                new() {
                    { "id", fileId },
                    { "title", title }
                }
            };
            using (MultipartFormDataContent form = new MultipartFormDataContent())
            {
                form.Add(new StringContent(JsonConvert.SerializeObject(files)), "files");
                if (channelIds != null && channelIds.Length > 0)
                    if (channelIds.Length == 1)
                        form.Add(new StringContent(channelIds[0]), "channel_id");
                    else
                        form.Add(new StringContent(string.Join(",", channelIds)), "channels");
                if (!string.IsNullOrEmpty(initialComment))
                    form.Add(new StringContent(initialComment), "initial_comment");
                if (!string.IsNullOrEmpty(thread_ts))
                    form.Add(new StringContent(thread_ts), "thread_ts");
                HttpResponseMessage response = PostRequestAsync(target.ToString(), form, APIToken).Result;
                string result = response.Content.ReadAsStringAsync().Result;
                callback(result.Deserialize<CompleteUploadExternalResponse>());
            }
        }

        public void UploadFile(Action<FileUploadResponse> callback, byte[] fileData, string fileName, string[] channelIds, string title = null, string initialComment = null, bool useAsync = false, string fileType = null, string thread_ts = null)
        {
            GetUploadURLExternal((uploadResponse =>
            {
                if (uploadResponse.ok)
                {
                    using (MultipartFormDataContent form = [])
                    {
                        form.Add(new ByteArrayContent(fileData), "file", fileName);
                        HttpResponseMessage response = PostRequestAsync(uploadResponse.upload_url, form, APIToken).Result;
                        string result = response.Content.ReadAsStringAsync().Result;
                    }
                    CompleteUploadExternal((completeResponse =>
                    {
                        if (completeResponse.ok)
                        {
                            GetFileInfo((fileInfoResponse =>
                            {
                                if (fileInfoResponse.ok)
                                {
                                    FileUploadResponse resp = new()
                                    {
                                        ok = true,
                                        file = fileInfoResponse.file
                                    };
                                    callback(resp);
                                }
                            }), completeResponse.files[0].id);
                        }
                        else
                        {
                            Console.WriteLine("update error: " + completeResponse.error);
                        }
                    }), uploadResponse.file_id, title ?? fileName, channelIds, initialComment, thread_ts);
                }
                else
                {
                    Console.WriteLine("upload error: " + uploadResponse.error);
                }
            }), fileName, fileData);


        }

        public void DeleteFile(Action<FileDeleteResponse> callback, string file = null)
        {
            if (string.IsNullOrEmpty(file))
                return;

            APIRequestWithToken(callback, new Tuple<string, string>("file", file));
        }

        public void PublishAppHomeTab(
            Action<AppHomeTabResponse> callback,
            string userId,
            View view)
        {
            view.type = ViewTypes.Home;
            var parameters = new List<Tuple<string, string>>
            {
                new Tuple<string, string>("user_id", userId),
                new Tuple<string, string>("view", JsonConvert.SerializeObject(view, Formatting.None,
                    new JsonSerializerSettings // Shouldn't include a not set property
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }))
            };

            APIRequestWithToken(callback, [.. parameters]);
        }
    }
}
