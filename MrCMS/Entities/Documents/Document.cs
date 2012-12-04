﻿using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MrCMS.Entities.Documents.Web;
using MrCMS.Models;
using MrCMS.Paging;
using MrCMS.Website;
using MrCMS.Helpers;
using NHibernate;

namespace MrCMS.Entities.Documents
{
    public abstract class Document : BaseEntity
    {
        [Required]
        public virtual string Name { get; set; }

        [Required]
        public virtual Document Parent { get; set; }
        [Required]
        [DisplayName("Display Order")]
        public virtual int DisplayOrder { get; set; }
        [Required]
        [DisplayName("Url Segment")]
        public virtual string UrlSegment { get; set; }

        public virtual string LiveUrlSegment
        {
            get { return MrCMSApplication.PublishedRootChildren.FirstOrDefault() == this ? string.Empty : UrlSegment; }
        }

        private IList<Document> _children = new List<Document>();
        private IList<Tag> _tags = new List<Tag>();

        public virtual IEnumerable<Webpage> PublishedChildren
        {
            get
            {
                return
                    Children.Select(webpage => webpage.Unproxy()).OfType<Webpage>().Where(document => document.Published)
                        .OrderBy(webpage => webpage.DisplayOrder);
            }
        }

        public virtual IList<Document> Children
        {
            get { return _children; }
            protected internal set { _children = value; }
        }

        public virtual IList<Tag> Tags
        {
            get { return _tags; }
            protected internal set { _tags = value; }
        }

        public virtual string TagList
        {
            get { return string.Join(", ", Tags.Select(x => x.Name)); }
        }

        public virtual int ParentId { get { return Parent == null ? 0 : Parent.Id; } }

        public virtual string DocumentType { get { return GetType().Name; } }

        /// <summary>
        /// Called before a document is to be deleted
        /// Place custom logic in here, or things that cannot be handled by NHibernate due to same table references
        /// </summary>
        public override void OnDeleting()
        {
            if (Parent != null)
            {
                Parent.Children.Remove(this);
            }
            base.OnDeleting();
        }

        public virtual void OnSaving(ISession session)  
        {

        }

        public virtual bool CanDelete
        {
            get { return !Children.Any(); }
        }

        protected internal virtual IList<DocumentVersion> Versions { get; set; }

        public virtual VersionsModel GetVersions(int page)
        {
            var documentVersions = Versions.OrderByDescending(version => version.CreatedOn).ToList();
            return
               new VersionsModel(
                   new PagedList<DocumentVersion>(
                       documentVersions, page, 10), Id);
        }
    }
}