﻿<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" 
				xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
				xmlns:msxsl="urn:schemas-microsoft-com:xslt" 
				exclude-result-prefixes="msxsl">
	
    <xsl:output method="text" indent="no"/>

    <xsl:template match="/">
		<xsl:text disable-output-escaping="yes">using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Content.Emoji
{
	/// &lt;summary&gt;
	/// Static class that provide methods for managing emojis.
	///
	/// Code is automatically generated by Waher.Utility.GetEmojiCatalog, which uses emoji source data from http://unicodey.com/emoji-data/table.htm
	/// &lt;/summary&gt;
	public static class EmojiUtilities
	{
		/// &lt;summary&gt;
		/// Tries to get information about an emoji, given its short name.
		/// &lt;/summary&gt;
		/// &lt;param name="ShortName">Emoji short name.&lt;/param&gt;
		/// &lt;param name="Emoji">Reference to emoji, if found, null otherwise.&lt;/param&gt;
		/// &lt;returns&gt;If the emoji was reconized.&lt;/returns&gt;
		public static bool TryGetEmoji(string ShortName, out Emoji Emoji)
		{
			if (ShortName.StartsWith(":"))
				ShortName = ShortName.Substring(1);

			if (ShortName.EndsWith(":"))
				ShortName = ShortName.Substring(0, ShortName.Length - 1);

			Emoji = null;

			switch (ShortName)
			{</xsl:text>
		<xsl:for-each select="/html/body/table/tbody/tr">
			<xsl:variable name="ShortName" select="td[position()=7]/."/>
			<xsl:text>
				case "</xsl:text>
			<xsl:value-of select="substring($ShortName,2,string-length($ShortName)-2)"/>
			<xsl:text>": Emoji = Emoji_</xsl:text>
			<xsl:value-of select="translate(substring($ShortName,2,string-length($ShortName)-2),'-+','_P')"/>
			<xsl:text>; break;</xsl:text>
		</xsl:for-each>
		<xsl:text>
			}
			
			return (Emoji != null);
		}</xsl:text>
		<xsl:for-each select="/html/body/table/tbody/tr">
			<xsl:variable name="ShortName" select="td[position()=7]/."/>
			<xsl:text disable-output-escaping="yes">

		/// &lt;summary&gt;
		/// </xsl:text>
			<xsl:value-of select="$ShortName"/>
			<xsl:text> </xsl:text>
			<xsl:value-of select="td[position()=5]/."/>
			<xsl:text> </xsl:text>
			<xsl:value-of select="td[position()=6]/."/>
			<xsl:text disable-output-escaping="yes">
		/// &lt;/summary&gt;
		public static readonly Emoji Emoji_</xsl:text>
			<xsl:value-of select="translate(substring($ShortName,2,string-length($ShortName)-2),'-+','_P')"/>
			<xsl:text> = new EmojiInfo("</xsl:text>
			<xsl:value-of select="substring($ShortName,2,string-length($ShortName)-2)"/>
			<xsl:text>", "</xsl:text>
			<xsl:value-of select="td[position()=14]/."/>
			<xsl:text>", "</xsl:text>
			<xsl:value-of select="td[position()=6]/."/>
			<xsl:text>", "</xsl:text>
			<xsl:value-of select="td[position()=5]/."/>
			<xsl:text>", </xsl:text>
			<xsl:choose>
				<xsl:when test="string-length(td[position()=1]/img/@src) > 0">
					<xsl:text>true</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>false</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:text>, </xsl:text>
			<xsl:choose>
				<xsl:when test="string-length(td[position()=2]/img/@src) > 0">
					<xsl:text>true</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>false</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:text>, </xsl:text>
			<xsl:choose>
				<xsl:when test="string-length(td[position()=3]/img/@src) > 0">
					<xsl:text>true</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>false</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:text>, </xsl:text>
			<xsl:choose>
				<xsl:when test="string-length(td[position()=4]/img/@src) > 0">
					<xsl:text>true</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>false</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:text>);</xsl:text>
		</xsl:for-each>
		<xsl:text disable-output-escaping="yes">
	}

	/// &lt;summary&gt;
	/// Contains information about an emoji.
	/// &lt;/summary&gt;
	public class EmojiInfo
	{
		private string shortName;
		private string fileName;
		private string description;
		private string unicode;
		private bool appleSupport;
		private bool googleSupport;
		private bool twitterSupport;
		private bool emoji1Support;

		/// &lt;summary&gt;
		/// Contains information about an emoji.
		/// &lt;/summary&gt;
		/// &lt;param name="ShortName">Emoji short name.&lt;/param&gt;
		/// &lt;param name="FileName">Emoji file name.&lt;/param&gt;
		/// &lt;param name="Description">Short description of emoji.&lt;/param&gt;
		/// &lt;param name="Unicode">Unicode representation of emoji.&lt;/param&gt;
		/// &lt;param name="AppleSupport">If the emoji is supported by Apple.&lt;/param&gt;
		/// &lt;param name="GoogleSupport">If the emoji is supported by Google.&lt;/param&gt;
		/// &lt;param name="TwitterSupport">If the emoji is supported by Twitter.&lt;/param&gt;
		/// &lt;param name="Emoji1Support">If the emoji is supported by emoji-1.&lt;/param&gt;
		public Emoji(string ShortName, string FileName, string Description, string Unicode, 
			bool AppleSupport, bool GoogleSupport, bool TwitterSupport, bool Emoji1Support)
		{
			this.shortName = ShortName;
			this.fileName = FileName;
			this.description = Description;
			this.unicode = Unicode;
			this.emoji1Support = Emoji1Support;
		}

		/// &lt;summary&gt;
		/// Emoji short name.
		/// &lt;/summary&gt;
		public string ShortName { get { return this.shortName; } }

		/// &lt;summary&gt;
		/// Emoji file name.
		/// &lt;/summary&gt;
		public string FileName { get { return this.fileName; } }

		/// &lt;summary&gt;
		/// Short description of emoji.
		/// &lt;/summary&gt;
		public string Description { get { return this.description; } }

		/// &lt;summary&gt;
		/// Unicode representation of emoji.
		/// &lt;/summary&gt;
		public string Unicode { get { return this.unicode; } }

		/// &lt;summary&gt;
		/// If the emoji is supported by Apple.
		/// &lt;/summary&gt;
		public bool AppleSupport { get { return this.appleSupport; } }

		/// &lt;summary&gt;
		/// If the emoji is supported by Google.
		/// &lt;/summary&gt;
		public bool GoogleSupport { get { return this.googleSupport; } }

		/// &lt;summary&gt;
		/// If the emoji is supported by Twitter.
		/// &lt;/summary&gt;
		public bool TwitterSupport { get { return this.twitterSupport; } }

		/// &lt;summary&gt;
		/// If the emoji is supported by emoji-1.
		/// &lt;/summary&gt;
		public bool Emoji1Support { get { return this.emoji1Support; } }
	}
}
</xsl:text>
    </xsl:template>
</xsl:stylesheet>