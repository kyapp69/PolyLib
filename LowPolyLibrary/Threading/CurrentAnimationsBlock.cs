using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using LowPolyLibrary.Animation;

namespace LowPolyLibrary.Threading
{
	public delegate void AnimationAddedEventHandler(object sender, EventArgs e);
	public delegate void NoPendingAnimationsEventHandler(object sender, EventArgs e);

	// Propagates data in a sliding window fashion.
	public class CurrentAnimationsBlock : IPropagatorBlock<AnimationBase, AnimationBase[]>, IReceivableSourceBlock<AnimationBase[]>
	{
		public event AnimationAddedEventHandler AnimationAdded;
		public event NoPendingAnimationsEventHandler NoPendingAnimations;

		// The target part of the block.
		private readonly ITargetBlock<AnimationBase> _mtarget;
		// The source part of the block.
		private readonly IReceivableSourceBlock<AnimationBase[]> _msource;

		private readonly BroadcastBlock<AnimationBase[]> _source;

		private List<AnimationBase> animList;
		private List<AnimationBase> toBeAdded;

        #region Constructors
        // Constructs a SlidingWindowBlock object.
        public CurrentAnimationsBlock() : this(new DataflowBlockOptions(), new ExecutionDataflowBlockOptions()) { }

		public CurrentAnimationsBlock(DataflowBlockOptions broadcastBlockOptions) : this(broadcastBlockOptions, new ExecutionDataflowBlockOptions()) { }

		public CurrentAnimationsBlock(ExecutionDataflowBlockOptions actionBlockOptions) : this(new DataflowBlockOptions(), actionBlockOptions) { }

		public CurrentAnimationsBlock(DataflowBlockOptions broadcastBlockOptions, ExecutionDataflowBlockOptions actionBlockOptions)
		{
			// Create a queue to hold messages.
			animList = new List<AnimationBase>();

            toBeAdded = new List<AnimationBase>();

			// The source part of the propagator holds arrays of size windowSize
			// and propagates data out to any connected targets.
		    _source = new BroadcastBlock<AnimationBase[]>(f => f, broadcastBlockOptions);

            // The target part receives data and adds them to the queue.
            ActionBlock<AnimationBase> target = new ActionBlock<AnimationBase>(item =>
			{
				//Signal that an animation was added
                RaiseAnimationAdded();
				//Add the item to the queue.
				toBeAdded.Add(item);
				var r = new AnimationBase[1];
				//if there is nothing in the 
				if (!_source.TryReceive(null, out r))
				{
					AddPendingAnimations();
					_source.Post(CurrentAnimations);
				}
				else
				{
					var tempAnim = r[0];
					if (tempAnim.CurrentFrame == tempAnim.numFrames)
					{
						AddPendingAnimations();
						_source.Post(CurrentAnimations);
					}
				}
			},actionBlockOptions);

            // When the target is set to the completed state, propagate out any
            // remaining data and set the source to the completed state.
            target.Completion.ContinueWith(delegate
            {
                _source.Complete();
            });

            _mtarget = target;
			_msource = _source;
		}
		#endregion

		private AnimationBase[] CurrentAnimations
		{
			get {
				//var lis = new List<AnimationBase>();
				//foreach (var key in animList.Keys)
				//{
				//	var anim = animList[key];
				//	lis.Add((T)Convert.ChangeType(anim, typeof(T)));
				//}
				//return lis.ToArray();
			    return animList.ToArray();
			}
		}

		private void IncrementAnimations()
		{
            //for each animation
            var removeList = new List<AnimationBase>();
            foreach (var t in animList)
            {
                var anim = t as AnimationBase;
                if (anim == null)
                    continue;
                //increment the animations current frame
                ++anim.CurrentFrame;
                if (anim.CurrentFrame >= anim.numFrames)
                {
                    removeList.Add(t);
                }
            }
            //if there aren't any elements to remove, dont try!
		    if (removeList.Count <= 0)
                return;
            //remove elements from animList that are in removeList
		    animList.RemoveAll(x => removeList.Contains(x));
		}

	    private void AddPendingAnimations()
	    {
            animList.AddRange(toBeAdded);
            toBeAdded.Clear();
        }

		public void FrameRendered()
		{
			IncrementAnimations();
            AddPendingAnimations();
			if (animList.Count > 0)
			{
				_source.Post(CurrentAnimations);
			}
			else 
			{
				RaiseNoPendingAnimations();
			}
		}

		#region Event Raising
		private void RaiseAnimationAdded(EventArgs e)
		{
			if (AnimationAdded != null)
			{
				AnimationAdded(this, e);
			}
		}

		private void RaiseAnimationAdded()
		{
			RaiseAnimationAdded(new EventArgs());
		}

		private void RaiseNoPendingAnimations(EventArgs e)
		{
			if (NoPendingAnimations != null)
			{
				NoPendingAnimations(this, e);
			}
		}

		private void RaiseNoPendingAnimations()
		{
			RaiseNoPendingAnimations(new EventArgs());
		}
		#endregion

		#region IReceivableSourceBlock<TOutput> members

		// Attempts to synchronously receive an item from the source.
		public bool TryReceive(Predicate<AnimationBase[]> filter, out AnimationBase[] item)
		{
			return _msource.TryReceive(filter, out item);
		}

		// Attempts to remove all available elements from the source into a new 
		// array that is returned.
		public bool TryReceiveAll(out IList<AnimationBase[]> items)
		{
			return _msource.TryReceiveAll(out items);
		}

		#endregion

		#region ISourceBlock<TOutput> members

		// Links this dataflow block to the provided target.
		public IDisposable LinkTo(ITargetBlock<AnimationBase[]> target, DataflowLinkOptions linkOptions)
		{
			return _msource.LinkTo(target, linkOptions);
		}

		// Called by a target to reserve a message previously offered by a source 
		// but not yet consumed by this target.
		bool ISourceBlock<AnimationBase[]>.ReserveMessage(DataflowMessageHeader messageHeader,
		   ITargetBlock<AnimationBase[]> target)
		{
			return _msource.ReserveMessage(messageHeader, target);
		}

		// Called by a target to consume a previously offered message from a source.
		AnimationBase[] ISourceBlock<AnimationBase[]>.ConsumeMessage(DataflowMessageHeader messageHeader,
		   ITargetBlock<AnimationBase[]> target, out bool messageConsumed)
		{
			return _msource.ConsumeMessage(messageHeader,
			   target, out messageConsumed);
		}

		// Called by a target to release a previously reserved message from a source.
		void ISourceBlock<AnimationBase[]>.ReleaseReservation(DataflowMessageHeader messageHeader,
		   ITargetBlock<AnimationBase[]> target)
		{
			_msource.ReleaseReservation(messageHeader, target);
		}

		#endregion

		#region ITargetBlock<TInput> members

		// Asynchronously passes a message to the target block, giving the target the 
		// opportunity to consume the message.
		DataflowMessageStatus ITargetBlock<AnimationBase>.OfferMessage(DataflowMessageHeader messageHeader,
		   AnimationBase messageValue, ISourceBlock<AnimationBase> source, bool consumeToAccept)
		{
			return _mtarget.OfferMessage(messageHeader,
			   messageValue, source, consumeToAccept);
		}

		#endregion

		#region IDataflowBlock members

		// Gets a Task that represents the completion of this dataflow block.
		public Task Completion { get { return _msource.Completion; } }

		// Signals to this target block that it should not accept any more messages, 
		// nor consume postponed messages. 
		public void Complete()
		{
			_mtarget.Complete();
		}

		public void Fault(Exception error)
		{
			_mtarget.Fault(error);
		}

		#endregion
	}
}